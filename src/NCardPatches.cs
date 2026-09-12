using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace EtherealGlow;

/// <summary>
/// Hooks the card view's existing refresh points rather than adding any polling.
///
/// ReloadOverlay runs whenever a card's model or affliction changes (it is the same place
/// the game builds its own card overlays), and UpdateVisuals runs on every pile/preview
/// change. OnFreedToPool is where the card releases its own glow nodes, so it is where we
/// release ours.
/// </summary>
[HarmonyPatch(typeof(NCard))]
internal static class NCardPatches
{
    [HarmonyPostfix]
    [HarmonyPatch("ReloadOverlay")]
    private static void ReloadOverlay_Postfix(NCard __instance)
    {
        EtherealGlowController.Refresh(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    private static void UpdateVisuals_Postfix(NCard __instance)
    {
        EtherealGlowController.Refresh(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCard.OnFreedToPool))]
    private static void OnFreedToPool_Prefix(NCard __instance)
    {
        EtherealGlowController.Release(__instance);
    }
}
