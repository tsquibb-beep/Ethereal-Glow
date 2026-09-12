using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace EtherealGlow;

/// <summary>
/// Recolours the game's own card highlight for Ethereal cards.
///
/// The highlight colour is decided in NHandCardHolder.UpdateCard, which assigns the cyan
/// "playable" colour and then overrides it with red or gold for cards in those states. That
/// method also calls NCard.UpdateVisuals *before* setting the colour, so patching UpdateVisuals
/// would be too early — the game would immediately paint over us.
/// </summary>
[HarmonyPatch(typeof(NHandCardHolder))]
internal static class NHandCardHolderPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    private static void UpdateCard_Postfix(NHandCardHolder __instance)
    {
        EtherealGlowController.RecolorHighlight(__instance.CardNode);
    }
}
