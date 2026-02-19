# ToddlersTexturePatch

This tool mod applies a generic toddler body texture fallback to HAR races.

## What it patches

- Scans all `AlienRace.ThingDef_AlienRace` defs at startup.
- Updates `graphicPaths.body.ageGraphics` entries for:
  - baby life stages
  - toddler life stages
- Updates `graphicPaths.body.bodytypeGraphics` entries for:
  - baby body type
  - child body type (toddler fallback)
- All patched entries point to `Naked_Baby`.

Texture source is:

- `Textures/Naked_Baby_south.png`
- `Textures/Naked_Baby_north.png`
- `Textures/Naked_Baby_east.png`

## Build

From `tool_mods/ToddlersTexturePatch/Source`:

```powershell
dotnet build -c Release -p:GameVersion=1.6
```

Output DLL path:

- `tool_mods/ToddlersTexturePatch/1.6/Assemblies/ToddlersTexturePatch.dll`
