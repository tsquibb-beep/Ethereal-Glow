using Godot;

namespace EtherealGlow;

/// <summary>
/// Builds the smoky halo as a plain <see cref="ColorRect"/> driven entirely by a shader.
///
/// Deliberately no custom Node subclass: the Godot C# source generators do not run over
/// this mod assembly, so a subclass would never get its _Ready/_Process overrides called.
/// Everything here is either set imperatively or animated by TIME inside the shader.
/// </summary>
internal static class SmokyGlowVisual
{
    /// <summary>
    /// Animated fbm smoke, confined to a band straddling the card edge so it reads as
    /// vapour clinging to the card rather than a flat rectangle of fog.
    /// </summary>
    private const string ShaderCode = @"
shader_type canvas_item;
render_mode blend_add;

uniform vec4 smoke_color : source_color = vec4(0.56, 0.83, 1.0, 1.0);
uniform float intensity = 1.0;
uniform float speed = 0.35;
// Where the card edge sits in UV space: the rect is grown past the card, so this is < 0.5.
uniform float card_frac = 0.74;
uniform float band_width = 0.3;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; i++) {
        v += a * noise(p);
        p *= 2.02;
        a *= 0.5;
    }
    return v;
}

void fragment() {
    float t = TIME * speed;

    // Domain-warp the noise so the smoke curls instead of just scrolling.
    vec2 warp = vec2(
        fbm(UV * 3.0 + vec2(0.0, -t)),
        fbm(UV * 3.0 + vec2(t * 0.5, 0.3))
    );
    float smoke = fbm(UV * 4.0 + warp * 1.5 + vec2(0.0, -t * 1.3));

    // Square-ish distance field: 0 at centre, 1 at the rect edge.
    vec2 c = abs(UV - vec2(0.5)) * 2.0;
    float d = max(c.x, c.y);

    // Band peaking at the card edge, falling off inwards and outwards.
    float inner = smoothstep(card_frac - band_width, card_frac, d);
    float outer = 1.0 - smoothstep(card_frac, 1.0, d);
    float band = inner * outer;

    // Slow breathing pulse keeps it alive when the card is stationary.
    float pulse = 0.85 + 0.15 * sin(TIME * speed * 2.0);

    float alpha = smoke * band * intensity * pulse;
    COLOR = vec4(smoke_color.rgb, clamp(alpha, 0.0, 1.0) * smoke_color.a);
}
";

    private static Shader? _shader;

    private static Shader Shader => _shader ??= new Shader { Code = ShaderCode };

    /// <summary>
    /// Creates a halo sized to <paramref name="cardSize"/>, grown outwards by the configured
    /// margin and offset so it stays centred on the card.
    /// </summary>
    public static ColorRect Create(Vector2 cardSize, GlowConfig config)
    {
        float margin = Mathf.Max(config.Margin, 0.0f);
        Vector2 size = cardSize * (1.0f + margin * 2.0f);

        var material = new ShaderMaterial { Shader = Shader };
        material.SetShaderParameter("smoke_color", config.SmokeColor);
        material.SetShaderParameter("intensity", Mathf.Max(config.Intensity, 0.0f));
        material.SetShaderParameter("speed", Mathf.Max(config.Speed, 0.0f));
        material.SetShaderParameter("card_frac", 1.0f / (1.0f + margin * 2.0f));
        material.SetShaderParameter("band_width", Mathf.Clamp(config.BandWidth, 0.01f, 1.0f));

        var rect = new ColorRect
        {
            Name = "EtherealGlow",
            Material = material,
            Size = size,
            Position = -cardSize * margin,
            // The shader supplies all colour; the base fill must not tint anything.
            Color = new Color(1, 1, 1, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        };

        return rect;
    }

    /// <summary>Fades the halo in. Safe to call only once the node is inside the tree.</summary>
    public static void FadeIn(ColorRect rect, GlowConfig config)
    {
        float duration = Mathf.Max(config.FadeInSeconds, 0.0f);
        if (duration <= 0.0f)
        {
            rect.Modulate = new Color(1, 1, 1, 1);
            return;
        }

        rect.CreateTween()
            .TweenProperty(rect, "modulate:a", 1.0f, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>Fades the halo out and frees it, mirroring how the game retires rarity glows.</summary>
    public static void Kill(ColorRect rect)
    {
        if (!GodotObject.IsInstanceValid(rect))
        {
            return;
        }

        if (!rect.IsInsideTree())
        {
            rect.QueueFree();
            return;
        }

        Tween tween = rect.CreateTween();
        tween.TweenProperty(rect, "modulate:a", 0.0f, 0.25);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(rect))
            {
                rect.QueueFree();
            }
        }));
    }
}
