# Ethereal Glow

A small visual mod for **Slay the Spire 2** that wraps every Ethereal card in an animated
smoky halo, so you can spot them in hand at a glance instead of reading keyword text.

Purely cosmetic — no gameplay is touched (`affects_gameplay: false`).

## Install

```bash
./deploy.sh                                     # uses the default Steam library path
./deploy.sh "/path/to/Slay the Spire 2"         # or point it at your install
```

This builds `EtherealGlow.dll` and copies it, the manifest, and a default config into
`<game>/mods/EtherealGlow/`. Launch the game and accept the mod warning.

To uninstall, delete that folder.

## Configuration

`mods/EtherealGlow/EtherealGlow.config.json` is read once at startup. `deploy.sh` never
overwrites an existing config.

| Key | Default | Meaning |
| --- | --- | --- |
| `enabled` | `true` | Master switch. |
| `color` | `"#b8e8ff"` | Smoke colour, any Godot-parsable hex. |
| `rimColor` | `"#dde4ea"` | Colour of the shimmering border. |
| `intensity` | `1.0` | Overall opacity multiplier. `0` is invisible. |
| `speed` | `0.35` | How fast the smoke churns and the shimmer travels. `0` freezes both. |
| `veilStrength` | `0.5` | Smoke banked against the inside of the card edge. |
| `hazeStrength` | `0.1` | Light haze across the whole card face. Keep low so the art stays readable. |
| `rimStrength` | `1.0` | Brightness of the border. |
| `rimWidth` | `0.018` | Border thickness, as a fraction of card height. |
| `edgeDepth` | `0.16` | How far the edge smoke reaches inward, as a fraction of card height. |
| `cornerRadius` | `0.05` | Corner rounding of the border, as a fraction of card height. |
| `drawUnderCost` | `true` | Draw the overlay below the energy/star cost gems so the costs stay crisp. |
| `recolorHighlight` | `true` | Replace the game's cyan card highlight with `highlightColor` on Ethereal cards. |
| `highlightColor` | `"#b9c6d0"` | Colour replacing that cyan. |
| `blur` | `0.003` | Softens the smoke's edges, as a fraction of card height (~1px on a standard card). `0` disables it. |
| `fadeInSeconds` | `0.35` | Fade-in time when a card becomes Ethereal. |

To make the effect louder, raise `rimStrength` and `veilStrength` first; `intensity` scales
everything at once. For softer, wispier smoke raise `blur` — it costs four extra shader taps,
so set it to `0` to skip the blur entirely.

A bad or missing config falls back to these defaults rather than failing to load.

## How it works

The game already renders per-rarity glows (`NCardRareGlow`, `NCardUncommonGlow`) as children
of `NCard.Body`, so this mod follows that pattern rather than inventing its own.

- **Hook points** — Harmony postfixes on `NCard.ReloadOverlay` (runs whenever a card's model
  or affliction changes) and `NCard.UpdateVisuals` (pile/preview changes), plus a prefix on
  `NCard.OnFreedToPool` for teardown. No polling, no `_Process`.
- **Ethereal test** — `CardModel.Keywords.Contains(CardKeyword.Ethereal)`, which already
  accounts for globally-granted keywords and other mods' `ModifyKeywordsInCombat` hooks.
  The mod also subscribes to `CardModel.KeywordsChanged`, so cards that gain or lose Ethereal
  mid-combat (Sculpting Strike, Void Form, Hexed) update immediately.
- **The effect** — one `ColorRect` stretched over the card with a runtime-compiled shader:
  domain-warped fbm smoke banked against the inside of the card edge, tent-blurred before the
  contrast push so the wisps feather rather than cut hard, plus a shimmering rim tracing a
  rounded-rectangle SDF around the silhouette. No texture or `.pck` assets, so
  nothing breaks when Mega Crit moves art around.
- **Draw order** — the overlay is added to `NCard.Body` above the card art, then slid down to
  the cost gems' index so the energy and star costs are not hazed over. The game's own rarity
  glows sit at index 1, which is *behind* the art: fine for a halo that only shows outside the
  card, useless for an overlay. The overlay's rect is measured from `%Frame` rather than
  assumed, so it never spills onto adjacent tooltips.
- **The card highlight** — the cyan outline the game draws around playable cards is its own
  `NCardHighlight`, coloured in `NHandCardHolder.UpdateCard`. A postfix there repaints it for
  Ethereal cards. Only the plain cyan is replaced: the red (cannot play) and gold states carry
  information the player needs, so cards in those states keep the game's colour.
- **Pooling** — `NCard` instances are recycled, so glow state lives in a
  `ConditionalWeakTable` keyed by the node and is released explicitly on pool free.

## Development

Requires the .NET SDK. The build references the assemblies shipped with the game
(`sts2.dll`, `GodotSharp.dll`, `0Harmony.dll`) directly and copies none of them.

```bash
dotnet build -c Release
dotnet build -c Release -p:Sts2Dir="/path/to/Slay the Spire 2"
```

To read the game's API, decompile the shipped assembly:

```bash
dotnet tool install -g ilspycmd
DOTNET_ROOT="$HOME/.dotnet" ilspycmd "<game>/data_sts2_windows_x86_64/sts2.dll" -p -o ./decompiled
```

`data_sts2_windows_x86_64/sts2.xml` also ships 5 MB of XML doc comments for the public API.

## Licence

MIT — see [LICENSE](LICENSE).

## Compatibility

Built and verified against **StS2 v0.107.1**. The game is in Early Access and mods break
often; `min_game_version` in the manifest is set accordingly.
