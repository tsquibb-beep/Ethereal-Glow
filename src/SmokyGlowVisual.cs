using Godot;

namespace EtherealGlow;

/// <summary>
/// Builds the Ethereal marker as a single <see cref="ColorRect"/> stretched over the card and
/// driven entirely by a shader: churning smoke banked against the inside of the card edge,
/// plus a shimmering rim tracing the card's rounded-rectangle silhouette.
///
/// Deliberately no custom Node subclass: the Godot C# source generators do not run over this
/// mod assembly, so a subclass would never get its _Ready/_Process overrides called. Everything
/// here is either set imperatively or animated by TIME inside the shader.
///
/// The shader works in normalised card space (corrected by an aspect uniform) rather than
/// pixels, so the node only needs its rect set once at creation.
/// </summary>
internal static class SmokyGlowVisual
{
    private const string ShaderCode = @"
shader_type canvas_item;
render_mode blend_mix;

uniform vec4 smoke_color : source_color = vec4(0.72, 0.91, 1.0, 1.0);
uniform vec4 rim_color : source_color = vec4(0.87, 0.91, 0.95, 1.0);
uniform float intensity = 1.0;
uniform float speed = 0.35;
uniform float veil_strength = 0.5;
uniform float haze_strength = 0.1;
uniform float rim_strength = 1.0;
// All lengths below are fractions of the card's height.
uniform float rim_width = 0.018;
uniform float edge_depth = 0.16;
uniform float corner_radius = 0.05;
uniform float blur = 0.003;
uniform float aspect = 0.72;

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

// One sample of the churning smoke field. Split out so it can be multi-tapped for the blur;
// the domain warp is computed once by the caller since it is low-frequency enough not to
// need blurring itself.
float smoke_field(vec2 uv, vec2 warp, float t) {
    return fbm(uv * 4.0 + warp * 1.6 + vec2(0.0, -t * 1.3));
}

// Signed distance to a rounded box: negative inside, zero on the outline.
float sd_round_box(vec2 p, vec2 b, float r) {
    vec2 q = abs(p) - b + vec2(r);
    return min(max(q.x, q.y), 0.0) + length(max(q, vec2(0.0))) - r;
}

void fragment() {
    float t = TIME * speed;

    vec2 p = (UV - vec2(0.5)) * vec2(aspect, 1.0);
    vec2 half_size = vec2(aspect, 1.0) * 0.5;
    float d = sd_round_box(p, half_size - vec2(rim_width * 0.5), corner_radius);

    // Domain-warped noise so the smoke curls instead of just scrolling.
    vec2 warp = vec2(
        fbm(UV * 3.0 + vec2(0.0, -t)),
        fbm(UV * 3.0 + vec2(t * 0.5, 0.3))
    );
    // Small tent-weighted blur of the noise field, so the contrast push below feathers the
    // wisp edges instead of cutting them hard. Offsets are corrected by aspect so the blur is
    // isotropic on screen rather than stretched across the card's width.
    float smoke;
    if (blur > 0.0001) {
        vec2 b = vec2(blur / max(aspect, 0.001), blur);
        smoke  = smoke_field(UV, warp, t) * 0.4;
        smoke += smoke_field(UV + vec2( b.x,  b.y), warp, t) * 0.15;
        smoke += smoke_field(UV + vec2(-b.x,  b.y), warp, t) * 0.15;
        smoke += smoke_field(UV + vec2( b.x, -b.y), warp, t) * 0.15;
        smoke += smoke_field(UV + vec2(-b.x, -b.y), warp, t) * 0.15;
    } else {
        smoke = smoke_field(UV, warp, t);
    }

    // Push the contrast up so it reads as distinct wisps, not flat fog.
    smoke = smoothstep(0.25, 0.95, smoke);

    float inside = 1.0 - smoothstep(0.0, 0.004, d);
    // Rises towards the card edge: heavy smoke banked against the border, clear in the middle
    // so the art and rules text stay readable.
    float edge = smoothstep(-edge_depth, 0.0, d);
    float smoke_alpha = (smoke * edge * veil_strength + smoke * haze_strength) * inside * intensity;

    // Shimmer travelling around the perimeter, roughened by noise so it glints unevenly.
    float ang = atan(p.y, p.x);
    float shimmer = 0.55 + 0.45 * sin(ang * 3.0 - TIME * speed * 5.0);
    float glint = 0.75 + 0.25 * fbm(vec2(ang * 2.0, TIME * speed * 2.0));
    float rim_line = 1.0 - smoothstep(0.0, rim_width, abs(d));
    float rim_alpha = rim_line * shimmer * glint * rim_strength * intensity;

    float total = smoke_alpha + rim_alpha;
    vec3 col = (smoke_color.rgb * smoke_alpha + rim_color.rgb * rim_alpha) / max(total, 0.0001);
    COLOR = vec4(col, clamp(total, 0.0, 1.0));
}
";

    private static Shader? _shader;

    private static Shader Shader => _shader ??= new Shader { Code = ShaderCode };

    /// <summary>
    /// Creates the overlay. The caller sets its rect via <see cref="SetAspect"/> and by
    /// assigning Position/Size, then adds it as the last child of the card body.
    /// </summary>
    public static ColorRect Create(GlowConfig config)
    {
        var material = new ShaderMaterial { Shader = Shader };
        material.SetShaderParameter("smoke_color", config.SmokeColor);
        material.SetShaderParameter("rim_color", config.RimColor);
        material.SetShaderParameter("intensity", Mathf.Max(config.Intensity, 0.0f));
        material.SetShaderParameter("speed", Mathf.Max(config.Speed, 0.0f));
        material.SetShaderParameter("veil_strength", Mathf.Max(config.VeilStrength, 0.0f));
        material.SetShaderParameter("haze_strength", Mathf.Max(config.HazeStrength, 0.0f));
        material.SetShaderParameter("rim_strength", Mathf.Max(config.RimStrength, 0.0f));
        material.SetShaderParameter("rim_width", Mathf.Clamp(config.RimWidth, 0.001f, 0.5f));
        material.SetShaderParameter("edge_depth", Mathf.Clamp(config.EdgeDepth, 0.01f, 1.0f));
        material.SetShaderParameter("corner_radius", Mathf.Clamp(config.CornerRadius, 0.0f, 0.5f));
        material.SetShaderParameter("blur", Mathf.Clamp(config.Blur, 0.0f, 0.1f));
        material.SetShaderParameter("aspect", 0.711f);

        // The rect is assigned explicitly by the caller rather than anchored to the parent:
        // SetAnchorsPreset keeps the control's existing rect, which for a freshly built node
        // is 0x0 - it anchors correctly and then renders nothing at all.
        return new ColorRect
        {
            Name = "EtherealGlow",
            Material = material,
            Color = new Color(1, 1, 1, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        };
    }

    /// <summary>Keeps the shader's aspect correction in step with the node's rect.</summary>
    public static void SetAspect(ColorRect rect, Vector2 size)
    {
        if (rect.Material is ShaderMaterial material && size.Y > 0.0f)
        {
            material.SetShaderParameter("aspect", size.X / size.Y);
        }
    }

    /// <summary>Fades the overlay in. Only valid once the node is inside the tree.</summary>
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

    /// <summary>Fades the overlay out and frees it, mirroring how the game retires rarity glows.</summary>
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
