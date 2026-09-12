using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace EtherealGlow;

/// <summary>
/// Entry point. The loader finds this via <see cref="ModInitializerAttribute"/> and calls
/// <see cref="Init"/> once, before any card view exists.
/// </summary>
[ModInitializer(nameof(Init))]
internal static class EtherealGlowMod
{
    private const string HarmonyId = "tomasapan.EtherealGlow";

    private static void Init()
    {
        try
        {
            new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());

            GlowConfig config = GlowConfig.Current;
            Log.Info($"[EtherealGlow] Initialised (enabled={config.Enabled}, colour={config.Color}, intensity={config.Intensity}).");
        }
        catch (Exception ex)
        {
            Log.Error($"[EtherealGlow] Failed to initialise: {ex}");
        }
    }
}
