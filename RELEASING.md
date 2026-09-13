# Releasing

## Cut a release

1. Bump `version.txt`, and keep `<Version>` in `EtherealGlow.csproj` and `"version"` in
   `EtherealGlow.json` in step with it. `package.sh` refuses to build if the manifest and
   `version.txt` disagree, because the manifest version is what players see in the game's
   mod list.
2. `./package.sh` → `dist/EtherealGlow-<version>.zip`.
3. Commit, `git tag vX.Y.Z`, `git push && git push --tags`.

The archive wraps everything in a single `EtherealGlow/` folder:

```
EtherealGlow/EtherealGlow.dll
EtherealGlow/EtherealGlow.json
EtherealGlow/EtherealGlow.config.jsonc
EtherealGlow/README.md
EtherealGlow/LICENSE
```

That layout is not arbitrary. The Slay the Spire 2 Vortex extension installs a mod containing
a `.dll` into `mods/<folder named after the dll>`, and the game's own loader looks for
`mods/<id>/<id>.dll`. The same archive therefore works through Vortex and for anyone
extracting it by hand. It also matches how existing StS2 mods on Nexus are packaged — verified
against the Skada Damage Meter archive.

**Only `.zip` is supported** by the StS2 Vortex extension. Do not ship `.7z` or `.rar`.

## Publish to Nexus Mods

The game lives at <https://www.nexusmods.com/slaythespire2>. Uploading needs a signed-in Nexus account, so
it is a manual step.

1. Log in, go to the Slay the Spire 2 page → **Upload mod**.
2. Name `Ethereal Glow`, category something like *User Interface* / *Visuals*.
3. Fill in the description (draft below), tick that it works with Vortex, and add a screenshot
   or two — the in-hand shot showing several cards is the one that sells it.
4. Under **Files**, upload `dist/EtherealGlow-<version>.zip` as a *Main file*, with the version
   matching `version.txt`.
5. Publish. Friends can then hit **Mod Manager Download** and Vortex does the rest.

For later versions, add a new file and set it as the main download rather than replacing the
old one, so Vortex's update detection works.

### Description draft (Nexus BBCode)

```bbcode
[size=4]Ethereal cards, at a glance[/size]

Ethereal cards are easy to miss until they vanish at end of turn. This mod wraps them in
drifting smoke and a shimmering silver border, so you can spot them in hand without reading
keyword text.

Purely cosmetic. It changes nothing about how the game plays.

[size=4]Features[/size]
[list]
[*]Animated smoke banked against the card edge, leaving the art and rules text readable
[*]A shimmering silver rim tracing the card border
[*]The game's cyan "playable" highlight recoloured for Ethereal cards
[*]Updates instantly when a card gains or loses Ethereal mid-combat
[*]Card costs stay crisp — the effect is drawn underneath them
[/list]

[size=4]Installation[/size]

Install with Vortex, or extract the [b]EtherealGlow[/b] folder into the game's [b]mods[/b]
folder so you have [b]mods\EtherealGlow\EtherealGlow.dll[/b]. Accept the mod warning on launch.

[size=4]Configuration[/size]

Every colour, strength and speed is adjustable in [b]EtherealGlow.config.jsonc[/b]. Copy that
file to [b]%APPDATA%\SlayTheSpire2\[/b] and your settings will survive mod updates.

[size=4]Compatibility[/size]

Built against Slay the Spire 2 v0.107.1. The game is in Early Access and updates often break
mods — if a patch breaks it, say so in the comments.

Source: [url=https://github.com/tsquibb-beep/Ethereal-Glow]github.com/tsquibb-beep/Ethereal-Glow[/url] (MIT)
```
