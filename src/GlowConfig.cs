using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace EtherealGlow;

/// <summary>
/// Tunables for the Ethereal glow, read once at startup from EtherealGlow.config.json
/// sitting next to the mod DLL. Missing or malformed files fall back to these defaults,
/// so the mod never fails to load because of a bad config.
/// </summary>
internal sealed class GlowConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Hex colour of the smoke, e.g. "#b8e8ff".</summary>
    [JsonPropertyName("color")]
    public string ColorHex { get; set; } = "#b8e8ff";

    /// <summary>Hex colour of the shimmering border.</summary>
    [JsonPropertyName("rimColor")]
    public string RimColorHex { get; set; } = "#dde4ea";

    /// <summary>Overall opacity multiplier. 0 is invisible, 1 is the designed strength.</summary>
    [JsonPropertyName("intensity")]
    public float Intensity { get; set; } = 1.0f;

    /// <summary>How fast the smoke churns and the rim shimmer travels. 0 freezes both.</summary>
    [JsonPropertyName("speed")]
    public float Speed { get; set; } = 0.35f;

    /// <summary>Strength of the smoke banked against the inside of the card edge.</summary>
    [JsonPropertyName("veilStrength")]
    public float VeilStrength { get; set; } = 0.5f;

    /// <summary>Strength of the light haze across the whole card face. Keep low so art stays readable.</summary>
    [JsonPropertyName("hazeStrength")]
    public float HazeStrength { get; set; } = 0.1f;

    /// <summary>Brightness of the shimmering border.</summary>
    [JsonPropertyName("rimStrength")]
    public float RimStrength { get; set; } = 1.0f;

    /// <summary>Border thickness, as a fraction of card height.</summary>
    [JsonPropertyName("rimWidth")]
    public float RimWidth { get; set; } = 0.018f;

    /// <summary>How far the edge smoke reaches inward, as a fraction of card height.</summary>
    [JsonPropertyName("edgeDepth")]
    public float EdgeDepth { get; set; } = 0.16f;

    /// <summary>Corner rounding of the border, as a fraction of card height.</summary>
    [JsonPropertyName("cornerRadius")]
    public float CornerRadius { get; set; } = 0.05f;

    /// <summary>
    /// Softens the smoke's edges, as a fraction of card height. Roughly a Gaussian blur radius:
    /// 0.003 is about 1px on a standard card. 0 disables the blur (and its extra shader taps).
    /// </summary>
    [JsonPropertyName("blur")]
    public float Blur { get; set; } = 0.003f;

    [JsonPropertyName("fadeInSeconds")]
    public float FadeInSeconds { get; set; } = 0.35f;

    private static GlowConfig? _current;

    public static GlowConfig Current => _current ??= Load();

    public Color SmokeColor => ParseColor(ColorHex, "#b8e8ff");

    public Color RimColor => ParseColor(RimColorHex, "#dde4ea");

    private static Color ParseColor(string value, string fallback)
    {
        try
        {
            return new Color(value);
        }
        catch (Exception)
        {
            Log.Warn($"[EtherealGlow] '{value}' is not a valid colour, falling back to {fallback}.");
            return new Color(fallback);
        }
    }

    private static GlowConfig Load()
    {
        string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
        {
            return new GlowConfig();
        }

        string path = Path.Combine(dir, "EtherealGlow.config.json");
        if (!File.Exists(path))
        {
            Log.Info("[EtherealGlow] No config file found, using defaults.");
            return new GlowConfig();
        }

        try
        {
            GlowConfig? loaded = JsonSerializer.Deserialize<GlowConfig>(File.ReadAllText(path));
            if (loaded == null)
            {
                return new GlowConfig();
            }

            Log.Info($"[EtherealGlow] Loaded config from {path}.");
            return loaded;
        }
        catch (Exception ex)
        {
            Log.Warn($"[EtherealGlow] Could not read {path} ({ex.Message}), using defaults.");
            return new GlowConfig();
        }
    }
}
