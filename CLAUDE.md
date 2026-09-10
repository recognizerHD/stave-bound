# CLAUDE.md — Stavebound

Context for Claude Code sessions in this repo.

## What this is

**Stavebound** — a Valheim BepInEx mod. Two features, one system:

1. **Any-portal travel.** Interact with a portal, pick any portal in the world off the map, and it
   points there for everyone until someone re-aims it. Walking in travels. Rewire, not per-player
   station mode — see DESIGN.md §5, and §13 for what was deferred.
2. **Site clearance.** Each portal site has a clearance mask built from physical staves bought with
   boss trophies, and a trip is checked against it. `MaterialFlow` picks which end answers —
   `Receive` the destination, `Deliver` the portal you leave, `Both` (default) either — so ore
   flows *inward* toward places you've invested in, outward to a frontier, or both ways once one
   end has paid. See DESIGN.md R3 and §10.

Feature 2 is the reason the mod exists. Feature 1 is table stakes (several mods already do it).

**Read `DESIGN.md` first.** It is the authoritative spec: rules R1–R7, the stave ladder, the
architecture table, phases, and §9's settled-vs-open decisions. This file only covers how to work in
the repo.

## Current state

**Phases 1 to 4 are built and played in game.** Any-portal travel, clearance decided by the
destination, the feedback that explains both, and optional seamless transit.

Working: the server-swept portal registry (`Portals/`), `stave_pid` identity that survives a relog,
one-way targets honoured by a `TeleportWorld.Teleport` prefix, the map-and-list selector with
clearance chips and a cargo filter (`UI/`), the `ObjectDB` tier map (`Tiers/`), six stave pieces
cloned from a dungeon prop with per-tier tinted cores (`Staves/`), the ten-second site sweep, the
travel gate with named refusals (`Travel/`), the inventory overlay, build-mode range and binding
feedback, and a trip that ends when loading does rather than on vanilla's timer.

Console commands, all echoing to the log: `stave_portals`, `stave_aim`, `stave_net`,
`stave_items`, `stave_prefabs` / `stave_inspect` / `stave_preview`. Reach for those before
inferring anything about a running game — several rounds were lost this way already.

**Clearance and `MaterialFlow` have both been confirmed on a real network**, which were the last two
correctness gates. `LogNetworkSync` now defaults off.

What remains is judgement, not correctness: the §4 costs are placeholders that have never been tuned,
and whether `Both` is the right shipped default is an open question about how a long game feels. Those
and the smaller standing gaps live in `TESTING.md` — read it before proposing anything be called done.

Deferred as niceties: destination thumbnails and selector favourites.

## Non-negotiable invariants

Violating any of these means rewriting a lot, so check against them before proposing a change:

- **Clearance is a property of a place, and one end of the trip must have paid for it.** Which end is
  asked is `MaterialFlow` — `Receive` the destination, `Deliver` the departure, `Both` (default)
  either — and every caller gets that answer from `ClearanceGate.EffectiveMask`, never by reading a
  mask itself. What no setting may do is let a tier through that *neither* end holds, or make
  clearance a property of a player rather than a place.
  (This replaces an earlier invariant reading "destination, never the source; no setting changes
  this". `Receive` is that rule, and is now one of three.)
- **The portal registry carries each portal's mask.** A client at A needs A's *target's* mask, and
  the target is normally unloaded on that client, so the ZDO mirror alone cannot answer it. This is
  what makes both the travel gate and the inventory overlay possible.
- **The server computes clearance masks; clients only read them.** A mask is written straight onto the
  portal's ZDO, recomputed from the staves standing in range — required, because a traveling client
  can read the destination portal's ZDO but cannot see staves kilometres away.
- **Never store a ZDOID.** The game renumbers every ZDO on every world load, so a saved ZDOID points
  at nothing — or at whatever inherited its number. Anything that must outlive a session refers to a
  portal by its `stave_pid`; the registry resolves that to a live ZDOID on demand. See DESIGN.md §12.
- **Never read or write a private game member directly.** Jotunn's publicised assemblies make
  `portal.m_nview` compile, but the real assembly is loaded at runtime and this game build's Mono
  throws `FieldAccessException` on every call — with no build-time warning. Patch methods take
  Harmony's `___fieldName` parameter; other callers use a cached `AccessTools.FieldRefAccess`. See
  DESIGN.md §12.
- **Never mutate `m_shared.m_teleportable`.** It's shared item data; changes leak into tooltips,
  other mods, and everything else that asks. Gate travel with a scoped context flag instead. This is
  the single most common bug in existing portal mods. The inventory overlay is the tempting place to
  break this — it is a visual state computed from the tier map, and touches no item data.
- **The blocked-item list is generated at runtime** by scanning `ObjectDB` for
  `m_teleportable == false`, mapped to tiers by config, with unknown items defaulting to the highest
  tier and being logged by name. Do not hardcode item lists.
- **Cargo checks are client-trusting** (player inventories are client-side in Valheim). The server
  owns *clearance*, never *cargo*. This is a co-op rule system, not anti-cheat — don't add
  complexity pretending otherwise.
- **Gamepad navigation in the map selector from the first commit.** Retrofitting Unity UI navigation
  is miserable.
- **The word *ward* is banned.** Valheim already has a piece called Ward, and the collision made the
  spec ambiguous. Ours are **staves**; the vanilla piece is the **guard stone**. See DESIGN.md §1.

## Before writing code that touches game internals

DESIGN.md §12 is split into **verified** and **still unverified**. The verified half was read off
`assembly_valheim.dll` with Mono.Cecil and can be built on directly. The unverified half needs the
game running (`ObjectDB` contents, prefab names, UI internals) — check those before use. If anything
doesn't match, fix `DESIGN.md` in the same commit.

Two verified facts worth knowing before you touch portals at all: the vanilla destination is a ZDO
*connection*, not the tag; and the server rebuilds every one of those connections from tag matches
every five seconds, so our one-way target has to live in our own key.

## Licensing rules for this repo

- Code is **MIT** (see DESIGN.md §11). Do **not** copy code from XPortal — it's GPL-3.0 and would
  relicense this whole assembly. Reading it for approach is fine.
- **Never commit game DLLs, publicised assemblies, or extracted game assets.** Reference the game
  install through an env var and keep those paths in `.gitignore`.
- Prefer cloning existing in-game prefabs at runtime over shipping custom models.

## Layout

`✓` exists, everything else is where the named concern goes when it's written.

```
Stavebound.sln              ✓
Directory.Build.props       ✓ game path + build guards; see "Build setup" below
DoPrebuild.props            ✓ Jotunn's publicise/MMHOOK prebuild toggle
Environment.props           ✗ gitignored, machine-local (copy from .example)
Stavebound/
  Stavebound.csproj         ✓
  BuildInfo.cs              ✓ GUID / name / version consts
  Plugin.cs                 ✓ BepInPlugin entry, Harmony bootstrap
  Config/                   ✓ ServerSync'd config, selector keys, translations
  Compat/                   ✓ conflicting-mod detection
  Tiers/                    ✓ ObjectDB scan, prefab -> tier map, the Clearance mask
  Portals/                  ✓ registry + server sync
  Staves/                   ✓ the six pieces, tinting, site resolution
  Travel/                   ✓ the teleport gate, refusals, cargo preview, transit
  UI/                       ✓ destination selector (keyboard + gamepad)
  Patches/                  ✓
Assets/                     ✗ asset bundle, only if we ever ship original art
DESIGN.md  CLAUDE.md  README.md  BUILDING.md  CHANGELOG.md  LICENSE
```

## Build setup — settled, don't re-litigate

Confirmed against the Jotunn NuGet package and the JotunnModStub template, not guessed:

- **`JotunnLib` 2.30.0** (2.29.2 predates Valheim 1.0), target framework **`net48`**, `LangVersion` 10.
- Jotunn's package supplies *every* reference — publicised game assemblies, BepInEx, `0Harmony`,
  all UnityEngine modules. Do not add game or BepInEx `<Reference>` entries by hand.
- **`BepInEx.AssemblyPublicizer.MSBuild` is not used.** Jotunn's own prebuild task publicises and
  generates MMHOOK, gated by `ExecutePrebuild` in `DoPrebuild.props`. The earlier note in this file
  calling for the publicizer package was wrong.
- `VALHEIM_INSTALL` comes from an env var or `Environment.props`; Jotunn derives `BEPINEX_PATH` and
  `VALHEIM_MANAGED` from it. `Directory.Build.props` imports it *before* NuGet's props, which is
  load-bearing — Jotunn resolves reference HintPaths at evaluation time, so setting the path later
  in the csproj silently yields references pointing at a guessed Steam path. It also defines
  `SolutionDir` so building the csproj directly behaves like building the solution.

Nothing in the build writes into the repo; the prebuild writes only into the game folder.

## Working notes

- Suggested plugin GUID: `com.recognizerhd.stavebound`.
- Detect conflicting mods by GUID at startup (Valheim Plus, Advanced Portals, Progression Portals,
  Gate of Ore-thority, unrestricted-portal mods) and log a loud warning — don't fight over patches.
- Costs in the stave ladder are **placeholders**. Don't treat them as balanced.
