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
        Vector2 cardSize = body.Size != Vector2.Zero ? body.Size : NCard.defaultSize;

        ColorRect glow = SmokyGlowVisual.Create(cardSize, config);
        body.AddChildSafely(glow);
        // Sit just above the card background but below the art and text, exactly where the
        // game puts its own rarity glows.
        body.MoveChildSafely(glow, 1);

        // AddChildSafely may defer, so only start the tween once the node is actually in the tree.
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(glow) && glow.IsInsideTree())
            {
                SmokyGlowVisual.FadeIn(glow, config);
            }
        }).CallDeferred();

        return glow;
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
        Action handler = () => Refresh(card);
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
