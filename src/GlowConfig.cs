using System;
using System.Collections.Generic;
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
    /// Draw the overlay below the card's energy and star cost gems, so the costs stay crisp.
    /// Ignored if the card art turns out to be drawn after the gems.
    /// </summary>
    [JsonPropertyName("drawUnderCost")]
    public bool DrawUnderCost { get; set; } = true;

    /// <summary>Replace the game's cyan card highlight with <see cref="HighlightColorHex"/>.</summary>
    [JsonPropertyName("recolorHighlight")]
    public bool RecolorHighlight { get; set; } = true;

    /// <summary>Hex colour replacing the game's cyan highlight on Ethereal cards.</summary>
    [JsonPropertyName("highlightColor")]
    public string HighlightColorHex { get; set; } = "#b9c6d0";

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

    public Color HighlightColor => ParseColor(HighlightColorHex, "#b9c6d0");

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

    private const string FileName = "EtherealGlow.config.jsonc";

    /// <summary>Legacy name, kept readable so existing installs do not lose their settings.</summary>
    private const string LegacyFileName = "EtherealGlow.config.json";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Looks for the config next to the game's save data first, then beside the mod DLL.
    ///
    /// The save-data copy wins so that settings survive a Vortex update, which replaces the
    /// whole mod folder. The file is .jsonc rather than .json deliberately: the game scans
    /// mods/ recursively for *.json and tries to parse every one as a mod manifest, logging
    /// an error for anything that is not one.
    /// </summary>
    private static GlowConfig Load()
    {
        foreach (string path in CandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                GlowConfig? loaded = JsonSerializer.Deserialize<GlowConfig>(File.ReadAllText(path), _jsonOptions);
                if (loaded != null)
                {
                    Log.Info($"[EtherealGlow] Loaded config from {path}.");
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[EtherealGlow] Could not read {path} ({ex.Message}), trying the next location.");
            }
        }

        Log.Info("[EtherealGlow] No config file found, using defaults.");
        return new GlowConfig();
    }

    private static IEnumerable<string> CandidatePaths()
    {
        string? userDir = null;
        try
        {
            userDir = OS.GetUserDataDir();
        }
        catch (Exception)
        {
            // Not fatal: fall back to the mod folder.
        }

        if (!string.IsNullOrEmpty(userDir))
        {
            yield return Path.Combine(userDir, FileName);
        }

        string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(modDir))
        {
            yield return Path.Combine(modDir, FileName);
            yield return Path.Combine(modDir, LegacyFileName);
        }
    }
}
