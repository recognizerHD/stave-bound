# Building Stavebound

Everything a contributor needs. Players want [README.md](README.md); the design and the reasoning
behind it are in [DESIGN.md](DESIGN.md).

## Setup

You need [BepInEx 5](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) installed
into Valheim, and the game run once so BepInEx generates its folders.

1. Tell the build where Valheim lives, either way round:
   - set `VALHEIM_INSTALL` as an environment variable, or
   - copy `Environment.props.example` to `Environment.props` and edit the path in it.

   `Environment.props` is gitignored on purpose — it is a machine-local path.

2. Build:

   ```sh
   dotnet build Stavebound.sln -c Release
   ```

   The first build runs Jotunn's prebuild task, which publicises the game assemblies and generates
   the MMHOOK assemblies **inside your game folder**. Nothing it produces belongs in this repo. Once
   that has run you can set `ExecutePrebuild` to `false` in `DoPrebuild.props` for faster builds; set
   it back to `true` after a game update.

3. The output is a single `Stavebound.dll`. Set `MOD_DEPLOYPATH` in `Environment.props` to have a
   successful build copy it straight into `BepInEx/plugins`.

If the game path is wrong or BepInEx is missing, the build stops with one plain error saying which,
rather than a hundred unresolved references.

### If you launch through a mod manager

r2modman and Thunderstore Mod Manager start Valheim with Doorstop pointed at the *profile's* BepInEx,
so a build copied into the game folder is never loaded. Point `MOD_DEPLOYPATH` at the profile instead:

```
%APPDATA%\r2modmanPlus-local\Valheim\profiles\<profile>\BepInEx\plugins\Stavebound
```

Getting this wrong costs an evening, because everything looks correct and nothing you change has any
effect.

## Checking the game API

**Nothing this mod calls in the game may be written from memory.** Prefab names, method signatures and
field visibility all have to be read off the shipped assemblies first — see
[DESIGN.md §12](DESIGN.md#12-game-api--verified-and-still-unverified), which is split into what has
been verified and what still needs the game running.

`tools/Dump-GameApi.ps1` reads metadata through the Mono.Cecil that BepInEx already ships, using the
same `VALHEIM_INSTALL` the build uses, and copies nothing out of the game folder.

```powershell
./tools/Dump-GameApi.ps1 -Type TeleportWorld
./tools/Dump-GameApi.ps1 -Type Game -IL ConnectPortals
./tools/Dump-GameApi.ps1 -Member IsTeleportable
```

Some things only exist at runtime — prefab names and models live in asset bundles, where no decompiler
reaches. Those are answered from inside a running game, and the answers echo to the log:

- `stave_prefabs <text>[,<text>…]` — search loaded prefabs, marked as pieces or items
- `stave_inspect <prefab>` — object tree, components, materials, shader colours
- `stave_preview <prefab>` — place a look-at-only copy of something vanilla `spawn` refuses

## Toolchain

| | |
|---|---|
| BepInEx | 5 (HarmonyX / `0Harmony` comes with it) |
| [Jotunn](https://github.com/Valheim-Modding/Jotunn) | `JotunnLib` 2.30.0 — pieces, localisation, config sync, and the game/BepInEx reference set |
| Target framework | `net48` |
| Publicising | Jotunn's own prebuild task. `BepInEx.AssemblyPublicizer.MSBuild` is **not** needed |

Two traps worth knowing before you write anything against the game:

- **Publicised assemblies are a compile-time fiction.** `portal.m_nview` compiles and then throws
  `FieldAccessException` at runtime, with no build warning. Patches take Harmony's `___fieldName`
  parameter; other callers use a cached `AccessTools.FieldRefAccess`.
- **A ZDOID is not a persistent reference.** The game renumbers every ZDO on every world load.
  Anything that must outlive a session refers to a portal by its `stave_pid`.

## Packaging a release

```powershell
./tools/Package.ps1
```

Builds Release and assembles a Thunderstore-ready zip in `dist/` — metadata at the root, the plugin
under `plugins/`, which is the layout Thunderstore expects and the one that quietly installs to the
wrong place if you get it wrong.

The version comes from `manifest.json`, which is the single source of truth; the script warns if
`BuildInfo.cs` disagrees.

`tools/Make-Icon.ps1` builds `icon.png` from a screenshot — centre-cropped to a square and downscaled
to the 256×256 Thunderstore requires, with resampling chosen because thin bright details are exactly
what glowing runes are and a naive downscale turns them to mush.

```powershell
./tools/Make-Icon.ps1 -Source shot.png -OffsetY -40
```

## Licensing rules

- Code is **MIT**. Do not copy code from XPortal — it is GPL-3.0 and would relicense this whole
  assembly. Reading it for approach is fine; see DESIGN.md §11 for where the line sits.
- **Never commit game DLLs, publicised assemblies, or extracted assets.** The build references your
  own local install through an env var, and those paths are gitignored.
- Prefer recolouring an existing in-game prefab over shipping art.
