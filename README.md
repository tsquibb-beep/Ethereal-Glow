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
| `color` | `"#8fd4ff"` | Smoke colour, any Godot-parsable hex. |
| `intensity` | `1.0` | Opacity multiplier. `0` is invisible. |
| `speed` | `0.35` | How fast the smoke churns. `0` freezes it. |
| `margin` | `0.35` | How far the halo bleeds past the card edge, as a fraction of card size. |
| `bandWidth` | `0.3` | Thickness of the smoke band hugging the edge. |
| `fadeInSeconds` | `0.35` | Fade-in time when a card becomes Ethereal. |

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
- **The effect** — one `ColorRect` with a runtime-compiled canvas shader: domain-warped fbm
  noise, masked to a band straddling the card edge, additively blended and slowly pulsing.
  No texture or `.pck` assets, so nothing breaks when Mega Crit moves art around.
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

## Compatibility

Built and verified against **StS2 v0.107.1**. The game is in Early Access and mods break
often; `min_game_version` in the manifest is set accordingly.
