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

        // Appended last, so it draws above the card art. The game's own rarity glows sit at
        // index 1 instead, which is *behind* the art - fine for a halo that only shows outside
        // the card silhouette, useless for an overlay.
        body.AddChildSafely(glow);

        // AddChildSafely may defer, and Control layout may not have settled yet, so re-sync
        // and start the tween once the node is actually in the tree.
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(glow) || !glow.IsInsideTree() || !GodotObject.IsInstanceValid(card))
            {
                return;
            }

            SyncRect(card, card.Body, glow);
            SmokyGlowVisual.FadeIn(glow, config);
        }).CallDeferred();

        return glow;
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
