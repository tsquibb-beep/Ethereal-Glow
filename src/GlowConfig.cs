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

    /// <summary>Hex colour of the smoke, e.g. "#8fd4ff".</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#8fd4ff";

    /// <summary>Overall opacity multiplier. 0 is invisible, 1 is the designed strength.</summary>
    [JsonPropertyName("intensity")]
    public float Intensity { get; set; } = 1.0f;

    /// <summary>How fast the smoke churns, in arbitrary units. 0 freezes it.</summary>
    [JsonPropertyName("speed")]
    public float Speed { get; set; } = 0.35f;

    /// <summary>How far the halo bleeds past the card edge, as a fraction of card size.</summary>
    [JsonPropertyName("margin")]
    public float Margin { get; set; } = 0.35f;

    /// <summary>Thickness of the smoke band hugging the card edge, in UV units.</summary>
    [JsonPropertyName("bandWidth")]
    public float BandWidth { get; set; } = 0.3f;

    [JsonPropertyName("fadeInSeconds")]
    public float FadeInSeconds { get; set; } = 0.35f;

    private static GlowConfig? _current;

    public static GlowConfig Current => _current ??= Load();

    public Color SmokeColor
    {
        get
        {
            try
            {
                return new Color(Color);
            }
            catch (Exception)
            {
                Log.Warn($"[EtherealGlow] '{Color}' is not a valid colour, falling back to #8fd4ff.");
                return new Color("#8fd4ff");
            }
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
