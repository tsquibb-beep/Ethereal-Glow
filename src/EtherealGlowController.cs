using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace EtherealGlow;

/// <summary>
/// Keeps one smoky halo in sync with each <see cref="NCard"/>'s Ethereal state.
///
/// NCard instances are pooled and reused, so state is held in a weak table keyed by the
/// node and torn down explicitly when the card is freed back to the pool.
/// </summary>
internal static class EtherealGlowController
{
    private sealed class Entry
    {
        public ColorRect? Glow;
        public CardModel? SubscribedModel;
        public Action? KeywordsChangedHandler;
    }

    private static readonly ConditionalWeakTable<NCard, Entry> _entries = new();

    /// <summary>One-shot diagnostic: makes an invisible overlay debuggable from the game log.</summary>
    private static bool _loggedAttach;

    /// <summary>One-shot diagnostic for the draw-order fix.</summary>
    private static bool _loggedOrder;

    /// <summary>
    /// Re-evaluates whether <paramref name="card"/> should be wearing a halo. Cheap enough to
    /// call from every visual refresh; creates and destroys nodes only on an actual change.
    /// </summary>
    public static void Refresh(NCard? card)
    {
        GlowConfig config = GlowConfig.Current;
        if (!config.Enabled || card == null || !GodotObject.IsInstanceValid(card) || !card.IsNodeReady())
        {
            return;
        }

        try
        {
            Entry entry = _entries.GetValue(card, _ => new Entry());
            CardModel? model = card.Model;

            if (!ReferenceEquals(entry.SubscribedModel, model))
            {
                Resubscribe(card, entry, model);
            }

            bool wantGlow = model != null && model.Keywords.Contains(CardKeyword.Ethereal);
            bool hasGlow = entry.Glow != null && GodotObject.IsInstanceValid(entry.Glow);

            if (wantGlow && !hasGlow)
            {
                entry.Glow = AttachGlow(card, config);
            }
            else if (wantGlow)
            {
                // Self-healing: if Control layout had not settled when the overlay was created,
                // this picks up the real rect on the next visual refresh.
                SyncRect(card, card.Body, entry.Glow!);
            }
            else if (!wantGlow && hasGlow)
            {
                SmokyGlowVisual.Kill(entry.Glow!);
                entry.Glow = null;
            }
        }
        catch (Exception ex)
        {
            // A visual mod must never take the game down with it.
            Log.Error($"[EtherealGlow] Failed to refresh glow: {ex}");
        }
    }

    /// <summary>
    /// Repaints the game's cyan "playable" highlight in the Ethereal colour.
    ///
    /// Only the plain playable colour is replaced: red (cannot play / warning) and gold both
    /// carry information the player needs, so cards in those states keep the game's colour.
    /// No cleanup is needed when a card stops being Ethereal, because UpdateCard reassigns the
    /// stock colour every time before this runs.
    /// </summary>
    public static void RecolorHighlight(NCard? card)
    {
        GlowConfig config = GlowConfig.Current;
        if (!config.Enabled || !config.RecolorHighlight)
        {
            return;
        }

        if (card == null || !GodotObject.IsInstanceValid(card) || !card.IsNodeReady())
        {
            return;
        }

        try
        {
            CardModel? model = card.Model;
            if (model == null || !model.Keywords.Contains(CardKeyword.Ethereal))
            {
                return;
            }

            NCardHighlight highlight = card.CardHighlight;
            if (highlight == null || !GodotObject.IsInstanceValid(highlight))
            {
                return;
            }

            if (!highlight.Modulate.IsEqualApprox(NCardHighlight.playableColor))
            {
                return;
            }

            highlight.Modulate = config.HighlightColor;
        }
        catch (Exception ex)
        {
            Log.Error($"[EtherealGlow] Failed to recolour highlight: {ex}");
        }
    }

    /// <summary>Drops all state for a card being recycled by the node pool.</summary>
    public static void Release(NCard? card)
    {
        if (card == null)
        {
            return;
        }

        if (!_entries.TryGetValue(card, out Entry? entry))
        {
            return;
        }

        Unsubscribe(entry);

        if (entry.Glow != null && GodotObject.IsInstanceValid(entry.Glow))
        {
            // The pool frees the card immediately, so skip the fade and drop the node now.
            entry.Glow.QueueFreeSafely();
        }

        entry.Glow = null;
        _entries.Remove(card);
    }

    private static ColorRect AttachGlow(NCard card, GlowConfig config)
    {
        Control body = card.Body;
        ColorRect glow = SmokyGlowVisual.Create(config);
        SyncRect(card, body, glow);

        // Appended last for now, so it draws above the card art. The game's own rarity glows
        // sit at index 1 instead, which is *behind* the art - fine for a halo that only shows
        // outside the card silhouette, useless for an overlay.
        body.AddChildSafely(glow);

        // AddChildSafely may defer, and Control layout may not have settled yet, so re-sync,
        // fix the draw order and start the tween once the node is actually in the tree.
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(glow) || !glow.IsInsideTree() || !GodotObject.IsInstanceValid(card))
            {
                return;
            }

            SyncRect(card, card.Body, glow);
            SinkBelowCost(card, card.Body, glow);
            SmokyGlowVisual.FadeIn(glow, config);
        }).CallDeferred();

        return glow;
    }

    /// <summary>
    /// Slides the overlay below the cost gems in draw order, so the energy and star costs stay
    /// crisp instead of being hazed over.
    ///
    /// The gems are ordinary nodes inside Body, drawn before an appended child. Moving the
    /// overlay to the gem's index puts the gem (and every sibling after it) on top. This is
    /// only safe if the card art sits *before* the gem: otherwise the same move would bury the
    /// overlay behind the art, so in that case the overlay is left where it is.
    /// </summary>
    private static void SinkBelowCost(NCard card, Control body, ColorRect glow)
    {
        if (!GlowConfig.Current.DrawUnderCost)
        {
            return;
        }

        int costIndex = IndexOfBodyChildContaining(body, card.GetNodeOrNull<Node>("%EnergyIcon"));
        int starIndex = IndexOfBodyChildContaining(body, card.GetNodeOrNull<Node>("%StarIcon"));
        if (starIndex >= 0 && (costIndex < 0 || starIndex < costIndex))
        {
            costIndex = starIndex;
        }

        int artIndex = IndexOfBodyChildContaining(body, card.GetNodeOrNull<Node>("%Frame"));
        bool canSink = costIndex >= 0 && artIndex >= 0 && artIndex < costIndex;

        if (canSink && glow.GetIndex() != costIndex)
        {
            body.MoveChildSafely(glow, costIndex);
        }

        if (!_loggedOrder)
        {
            _loggedOrder = true;
            Log.Info($"[EtherealGlow] Draw order: art={artIndex}, cost={costIndex}, children={body.GetChildCount()}, sank={canSink}, glow now at {glow.GetIndex()}.");
        }
    }

    /// <summary>
    /// Walks up from <paramref name="node"/> to whichever direct child of <paramref name="body"/>
    /// contains it, and returns that child's index. -1 if the node is not under Body at all.
    /// </summary>
    private static int IndexOfBodyChildContaining(Control body, Node? node)
    {
        Node? current = node;
        while (current != null && GodotObject.IsInstanceValid(current) && current.GetParent() != body)
        {
            current = current.GetParent();
        }

        return current != null && GodotObject.IsInstanceValid(current) ? current.GetIndex() : -1;
    }

    /// <summary>
    /// Matches the overlay to the card's real on-screen rect.
    ///
    /// The card's position and size inside Body are not documented and must not be guessed:
    /// this reads the game's own frame node ("%Frame", the card border art) and maps its
    /// corners into Body's local space, so the overlay lines up whatever the scene does and
    /// adapts if card dimensions change. Falls back to NCard.defaultSize at the origin only
    /// if the frame cannot be measured yet.
    /// </summary>
    private static void SyncRect(NCard card, Control body, ColorRect glow)
    {
        Vector2 position = Vector2.Zero;
        Vector2 size = NCard.defaultSize;
        bool measured = false;

        Control? frame = card.GetNodeOrNull<Control>("%Frame");
        if (frame != null && GodotObject.IsInstanceValid(frame) && frame.Size.X > 1.0f && frame.Size.Y > 1.0f)
        {
            Transform2D toBody = body.GetGlobalTransform().AffineInverse();
            Transform2D frameTransform = frame.GetGlobalTransform();
            Vector2 topLeft = toBody * (frameTransform * Vector2.Zero);
            Vector2 bottomRight = toBody * (frameTransform * frame.Size);

            Vector2 measuredSize = bottomRight - topLeft;
            if (measuredSize.X > 1.0f && measuredSize.Y > 1.0f)
            {
                position = topLeft;
                size = measuredSize;
                measured = true;
            }
        }

        if (glow.Position != position || glow.Size != size)
        {
            glow.Position = position;
            glow.Size = size;
            SmokyGlowVisual.SetAspect(glow, size);
        }

        if (!_loggedAttach)
        {
            _loggedAttach = true;
            Log.Info($"[EtherealGlow] First glow rect: pos={position}, size={size}, measured={measured}, body.Size={body.Size}.");
        }
    }

    private static void Resubscribe(NCard card, Entry entry, CardModel? model)
    {
        Unsubscribe(entry);

        if (model == null)
        {
            return;
        }

        // Ethereal can be granted or stripped mid-combat (Sculpting Strike, Void Form, Hexed),
        // and that fires no visual refresh of its own.
        //
        // The handler detaches itself if the card node has been freed without passing through
        // the pool, since models outlive card views and would otherwise keep calling into a
        // dead node forever.
        Action? handler = null;
        handler = () =>
        {
            if (!GodotObject.IsInstanceValid(card))
            {
                model.KeywordsChanged -= handler;
                return;
            }

            Refresh(card);
        };
        model.KeywordsChanged += handler;

        entry.SubscribedModel = model;
        entry.KeywordsChangedHandler = handler;
    }

    private static void Unsubscribe(Entry entry)
    {
        if (entry.SubscribedModel != null && entry.KeywordsChangedHandler != null)
        {
            entry.SubscribedModel.KeywordsChanged -= entry.KeywordsChangedHandler;
        }

        entry.SubscribedModel = null;
        entry.KeywordsChangedHandler = null;
    }
}
