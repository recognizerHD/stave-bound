# Stavebound — Design Spec

> A Valheim mod. Travel to any portal by name; what you may **carry** through is decided by the
> staves standing at the **destination**, and every stave is bought with a boss trophy.

Status: **Draft 5. Phases 1 to 4 are built and played.** Any-portal travel, clearance decided by the
destination, the feedback that explains both, and optional seamless transit. Everything in the build
order below is implemented and has been exercised in game.

One thing stands between that and a release, and it is not a feature: clearance has never crossed a
real network. The registry sync was proven on two machines before masks existed, so no client has yet
received a non-zero one.

The costs in §4 are settled — see §10, where the balance question turned out to be framed backwards.

---

## 1. The pitch

Two features, one system.

1. **Any-portal travel.** Interact with a portal, pick any portal in the world off the map, and it
   points there — for everyone — until someone re-aims it. Walking in travels.
2. **Destination clearance.** Each portal site has a clearance mask built out of physical
   staves. When you travel, the game checks the mask of the portal you are *arriving at* against
   what you are carrying.

The second one is the reason the mod exists. Build an Elder's Stave at your base and from then
on *every* portal in the world can send copper, tin and bronze **to** your base — and none of them
can receive it back until you go build a stave there too.

That asymmetry is the whole design: **ore flows inward** toward places you have invested in, and
outposts stay cheap, disposable and one-way.

### Words, used precisely

The word *ward* is banned from this document, because Valheim already has a piece called Ward and the
collision made everything ambiguous.

| Term | Means |
|---|---|
| **Stave** | One of our six clearance pieces — Elder's, Bonemass's, Moder's, Yagluth's, Queen's, Ashen. Built from a boss trophy plus the metal it unlocks. |
| **Site** | A portal and the staves standing near it. |
| **Clearance mask** | The per-tier flags the staves at a site grant, written onto its portal. |
| **Guard stone** | Valheim's *vanilla* piece — the one the game labels "Ward". Ours never touch it, except that `ReaimPermission` can read its permitted-players list (§5). |

---

## 2. Lineage

| Mod | Nexus | What we take |
|---|---|---|
| AnyPortal (+ XPortal, its maintained rewrite) | [170](https://www.nexusmods.com/valheim/mods/170) / [2239](https://www.nexusmods.com/valheim/mods/2239) | The destination list. Non-negotiable feature. |
| Handy portals | [471](https://www.nexusmods.com/valheim/mods/471) | Gamepad support and optional name hiding. Its station interaction model is *not* what we build — see §5, and §13 for why it was set aside. |
| Advanced Portals | [2187](https://www.nexusmods.com/valheim/mods/2187) | Per-portal cargo permissions — the *idea*, not the delivery. |
| Immersive Portals | [268](https://www.nexusmods.com/valheim/mods/268) | Seamless no-loading-screen transit. Optional, last phase. |

Note: Immersive Portals is **not** a see-through render of the far side (that's the Minecraft mod of
the same name). It removes the loading screen, preloads the destination and places you facing the
way you walked in, with a configurable fade.

### Prior art that already exists — don't reinvent it

- **XPortal** (GPLv3) already solves registry + sync + panel + map ping for the rewire model.
- **Progression Portals** ([2659](https://www.nexusmods.com/valheim/mods/2659)) and
  **Gate of Ore-thority** already gate blocked items on boss kills, globally per player.
- **ProperPortals / PortalPreload / QuickTeleport** already do loading-screen reduction.

**The gap nobody has filled:** clearance as a property of the *destination*, and as a thing you
*build* and can lose. Lead with that on the mod page; everything else is table stakes.

---

## 3. Rules

| # | Rule |
|---|---|
| **R1** | Each portal carries a **clearance mask** — independent per-tier flags, not a single level. A site with Elder's + Moder's staves accepts copper and silver but still refuses iron. Nothing forces you up the ladder in order. |
| **R2** | A site is **a portal and the staves standing near it**. Each stave grants its tier to the **nearest portal** within `StaveRadius` (default 10 m); `PortalBinding = AllInRadius` instead grants it to every portal in range, for a base spread across two portals. One relationship, resolved from position, storing nothing. |
| **R3** | Which end of a trip is checked is `MaterialFlow`, one rule for the whole world: `Receive` asks the destination, `Deliver` asks the portal you leave, `Both` (**the default**) asks either. Under all three, clearance stays a property of a **place** — somewhere on the trip has to have been paid for, and a tier neither end holds never moves. `Receive` is the original rule and the only one that keeps outposts one-way. |
| **R4** | Every stave costs that biome boss's **trophy** plus a little of **the metal it unlocks**. You always earn the shortcut by making the haul the hard way once. |
| **R5** | Trophies are farmable by re-summoning, so the ladder is a **cost curve, not a wall**. |
| **R6** | Refusals **name the reason**: not "you cannot teleport with that" but `Iron cannot enter "Copper Mine" — no Bonemass's Stave at that site.` |
| **R7** | Staves are **permanent** once built. There is no fuelled or consumable mode: the gate is the boss kill, not upkeep. |

---

## 4. The stave ladder

Every rune costs **the boss's head plus some of what that biome gives you** — see §10 for why that is
the whole of it, and why the ladder is not trying to be a wall.

There is no anchor. An earlier draft had a tier-0 **Wayfarer's Anchor** that granted nothing and was
required before any stave counted — it gave Eikthyr a job and made founding a site deliberate.
Both are real, and neither was worth the cost: a piece that opens nothing is a tax on starting, and a
player who has just killed the Elder and hauled ten copper across the map does not need a second
errand before their stave works. Cutting it also collapses R2 from two relationships to one.

| # | Piece | Cost | Opens | Why that boss |
|---|---|---|---|---|
| 1 | **Elder's Stave** | Elder trophy · 10 copper · 20 stone | Copper ore & bar, tin ore & bar, bronze | Black Forest metals. |
| 2 | **Bonemass's Stave** | Bonemass trophy · 10 iron · 20 stone | Iron scrap, iron | Swamp iron — the rule the idea started from. |
| 3 | **Moder's Stave** | Moder trophy · 10 silver · 20 stone | Silver ore, silver, dragon eggs | Mountain hauls are the worst ones; this is the stave people want most. |
| 4 | **Yagluth's Stave** | Yagluth trophy · 10 black metal · 20 stone | Black metal scrap, black metal | Plains fulings. |
| 5 | **Queen's Stave** | Queen trophy · 3 dvergr extractors · 20 stone | Dvergr extractors, mechanical springs | Added once the `ObjectDB` scan proved the Mistlands blocks resources — see below. The cost *is* the cargo: extractors are one of the things this rune unlocks, so three of them have to reach the site by boat and cart before any of them can ever come by portal. |
| 6 | **Ashen Stave** | Fader trophy · 10 flametal · 20 stone | Flametal ore & bar, Ashlands blocked items | Fader alone, now the Queen has her own rune. |

### The Mistlands does have blocked resources

This document claimed it didn't, and used that to justify folding the Queen into the Ashen Stave.
The premise was wrong. The `ObjectDB` scan reports **26** non-teleportable items on game 0.221.12, and
two of them — `DvergrNeedle` and `MechanicalSpring` — are Mistlands loot. `chest_hildir1/2/3` are
blocked too, and belong to no metal ladder at all.

**Settled: the ladder has six rungs, and the Queen gets her own.** Folding her into Ashen was only
ever justified by the belief that the Mistlands blocked nothing; once that was false, the folding took
the *items* with it — and Ashen also costs a Fader trophy, so moving a mechanical spring would have
meant finishing the Ashlands first, a biome past the one the item drops in. Every other rung works the
other way round: beat the boss, then haul that region's goods. A Queen's Stave restores that, and
Ashen drops to Fader alone, which is cleaner than a rune costing two bosses' trophies.

It costs dvergr extractors rather than a metal, because the Mistlands has no ore and inventing one
would make the rune read as a sixth smelter. That the cost is also one of the blocked items it
unlocks is the sharpest expression of R4 on the whole ladder: three extractors have to make the
journey by boat and cart before a fourth may ever come by portal. Every other rung asks for a metal
you happen to be carrying anyway; this one asks for the exact thing you are trying to stop carrying.

An earlier revision placed Mistlands loot under Yagluth's Stave instead. That was the right call
while the ladder had five rungs and the wrong one the moment it had six; recorded here because the
reasoning matters more than the conclusion.

Hildir's three quest chests follow the biome of the quest that drops each — bronze to Elder's, silver
to Moder's, brass to Yagluth's — rather than sitting at the top with the flametal. They are keepsakes,
and a decorative chest being harder to move than a bar of black metal reads as an oversight rather
than a rule.

None of this is code. The tier of any prefab is a config line, so all of it can be re-argued in §10
against real play without touching the mod.

### Do not hand-maintain the item list

Build it at runtime:

1. Scan `ObjectDB` for items with `m_shared.m_teleportable == false`.
2. Map known prefab names → tier from a config file.
3. Anything unrecognised → **highest tier**, and **log it by name** so a user can classify it.

That way a game update (Deep North, new content, other mods adding blocked items) never breaks the
mod; it just gets conservative until someone edits one line.

---

## 5. Interaction model

Settled: **rewire, selected on the map.** A portal's destination is a property of the portal and
applies to everyone.

### Two verbs

- **Walk in** — travel to wherever the portal currently points. No menu, no dialog.
- **Interact** — open the map selector and re-aim the portal. The new target stands for everyone
  until someone changes it again.

Splitting the verbs is what makes a shared destination tolerable. Travelling is the frequent action
and costs nothing; re-aiming is deliberate and rare.

### Selection is a map, with a list beside it

Choose the destination by picking a portal on the world map. Names stop carrying meaning at about a
dozen portals; position never does. This subsumes what was previously listed as a "map ping on the
selected row" — the map *is* the selector.

The list is shown **alongside** the map rather than instead of it, and the two are not alternative
selectors: there is one highlighted destination, and the map and the list are two renderings of it.
Moving the highlight moves both. That is why the interaction model is a highlight rather than a click
target — it costs nothing to add a second view of a highlight, and a great deal to reconcile two
independent ones.

They answer different questions, which is why neither replaces the other. The map answers *where is
it, and is that near the thing I care about*. The list answers *what are my options, in an order I
chose* — nearest first, or by name — and it is the only one of the two that stays usable when the
destination is off the visible map or has no name worth recognising.

### Pointers are one-way

Each portal independently stores where *it* sends you. A points at B; B may point at C rather than
back at A; C may point at B, which makes B and C a working pair — but only because two independent
pointers happen to face each other. Consequence: this does **not** require multi-portal rooms, since
you re-aim a portal instead of building another beside it.

### What it costs

A target is shared world state, so re-aiming changes everyone's route — including someone mid-haul.
That contention is a shipped property of the design now rather than something avoided. It is what
`ReaimPermission` below exists to bound, and the reason `PortalBinding = AllInRadius` is kept: if
re-aiming fights push players into building hubs after all, a per-portal charge would sting.

It also moves the clearance check. With a per-trip picker, choosing a destination and being told "no"
are the same moment. Here you commit by walking, so a refusal lands at the threshold with no dialog to
deliver it. The next two sections answer both problems: who is allowed to re-aim at all, and how a
player learns a destination will refuse them.

### Who may re-aim

`ReaimPermission`, server-synced, **default `Anyone`**:

| Value | Rule |
|---|---|
| `Anyone` | Any player may re-aim any portal. Default. |
| `GuardStonePermitted` | Inside a **guard stone**'s protected area, only players it permits. Outside any guard stone, anyone. |
| `Admin` | Admins only. |

`GuardStonePermitted` reuses the guard stone's existing permitted-players list rather than inventing
a second access-control system — players already understand it, and it already means "this is my
base, these are my crew". The guard stone is the vanilla piece the game labels *Ward*; it has nothing
to do with staves, which carry clearance and have no player list.

A `Builder` option was considered and dropped: it needs the portal to record who placed it, and that
is not worth a shippable-or-not dependency for a rule two other values already cover.

### Telling the player before they commit

Two layers, and the first is the one that matters:

1. **A blocked marker on the inventory slot.** Near a portal, any stack that the portal's *current
   destination* will refuse gets a blocked overlay on its icon. You find out while packing, not after
   walking — which is better than any doorway warning, because at that point you can still do
   something about it.
2. **A named message on entry.** If you walk in anyway, R6's refusal names the offending resource and
   the missing stave rather than saying "you cannot teleport with that".

Three constraints on the first layer, all of them load-bearing:

- **Proximity-gated.** The overlay is live only within `CargoPreviewRange` of a portal, reading that
  portal's target. Always-on would paint red across your ore for the entire game and train everyone
  to ignore it.
- **It reads the registry, not the far side's ZDO.** The destination is normally unloaded on this
  client — see §6.
- **It never touches item data.** The overlay is computed from the prefab→tier map against the
  destination mask. Flipping `m_shared.m_teleportable` to drive a UI state would leak into tooltips
  and every other mod, which is the failure mode §6 calls out.

Where two portals are close together, "the portal you are near" resolves the same way a stave picks
its portal: nearest within range.

### Selector requirements

- **Gamepad navigation from the first commit.** Retrofitting Unity UI navigation later is miserable.
- Per-portal **clearance chips** (Cu / Fe / Ag / Bm / Fl) — granted filled, missing dashed — plus a
  verdict and a footer showing what you're carrying and how many destinations will take it.
- Filter by **"only destinations that accept my cargo"**. Still meaningful under a shared target: you
  re-aim because you intend to travel with what you are holding.
- Sort/filter by distance, name and favourites for the list view that backs the map.
- `HidePortalNames` option, as Handy portals has.
- Keep vanilla tag pairing as the fallback when no explicit target is set, so an unmodded save
  behaves normally.

### Build-mode feedback — required, not polish

Auto-binding removes the binding UI, but it also removes the player's certainty about what the game
just decided for them. Two questions need answering without a menu:

- **Range** — which portal a stave reaches. Otherwise a rune planted 11 m out silently does nothing,
  and the player has no way to find out except by hauling ore and being refused.
- **Connection** — which portal a rune bound to. With two portals 30 m apart, nothing on screen says
  which one just became iron-capable.

Reuse the existing in-game guard stone effect for both rather than authoring new art — that keeps this
inside the §11 rule about cloning prefabs instead of shipping assets. The prefab and its component
names are unverified; see §12.

---

## 6. Architecture

| Concern | Approach |
|---|---|
| **Stack** | BepInEx 5 + HarmonyX. Jotunn for custom pieces, localisation, config sync. The six staves are custom build pieces — exactly `PieceManager`'s job. |
| **Where clearance lives** | An int bitmask written straight onto the portal's ZDO (`stave_mask`), recomputed from the staves standing in range. Writing it to the portal is **not optional**: a traveling client can read the destination portal's ZDO but cannot see staves 2 km away. |
| **Who computes it** | The **server**. It holds every ZDO, so it recomputes a site's mask on stave place/destroy and on a slow sweep, then writes the portals. Clients never author a mask. This also self-heals staleness when a stave is destroyed while the site is unloaded. |
| **Portal registry** | Server-authoritative list from `ZDOMan.GetPortals()`, pushed to clients on join and on change. **Each record carries that portal's clearance mask**, not just name, id and position — see the row below for why. `GetPortals()` hands back the live list, so read it and never mutate it. |
| **Where the target lives** | Our own `stave_target` ZDOID key on the portal's ZDO, with a prefix on `TeleportWorld.Teleport` preferring it over the vanilla destination. **Not** the vanilla `ConnectionType.Portal` connection: the server rebuilds those from tag matches every 5 seconds and would erase a one-way target almost immediately (§12). Leaving that pass alone is also what gives §5's vanilla-tag fallback for free — a portal we never re-aimed still pairs the way it always did. |
| **Why the registry carries masks** | A client standing at A needs the mask of A's *target*, which is usually kilometres away and not in the client's ZDO set at all. The ZDO mirror alone cannot answer it. So the mask travels in the registry record, which is what makes both the travel gate and the inventory preview possible without loading the far side. |
| **The check** | Prefix on `TeleportWorld.Teleport(Player)`: resolve destination → read mask → walk `Inventory.GetAllItems()` for `m_shared.m_teleportable == false` → allow, or refuse with a named reason through `Character.Message`. Suppress vanilla's `Humanoid.IsTeleportable()` via a scoped context flag, and honour the portal's own `m_allowAllItems`. |
| **What not to do** | Do **not** flip `m_shared.m_teleportable` on shared item data to let ore through. It's shared state — it leaks into tooltips, other mods, and anything else that asks. Several existing portal mods take that shortcut and it's why they conflict. |
| **Trust model** | Player inventories are client-side in Valheim, so cargo checks are client-trusting — same as vanilla. The server can authoritatively own **clearance**, never **cargo**. Put that in the readme: this is a rule system for a co-op server, not anti-cheat. |
| **Install** | Server **and** every client. Config syncs from the server so tiers can't be edited locally. |
| **Uninstalling** | Extra ZDO keys are harmless to a vanilla client. Custom pieces are not — remove the mod and every stave vanishes. Normal for custom-piece mods; warn anyway. |
| **Known conflicts** | Anything that rewrites teleport rules: Valheim Plus, Advanced Portals, Progression Portals, Gate of Ore-thority, unrestricted-portal mods. Detect by GUID at startup and log a loud warning rather than fighting over patches. |

---

## 7. The seamless-transit layer (Phase 4, default off)

One real collision: seamless transit removes the **Interact** moment, and the interact moment is
where the cargo check lives. If you walk in and the destination refuses your ore, there's no dialog
to refuse you in.

**Since §5 settled on rewire, this is no longer only a Phase 4 problem.** Interact now re-aims rather
than travels, so *every* trip is a walk-in and there is never a dialog to carry the refusal. The
approach-time warning below is the answer to the base game loop, not just to seamless transit — which
is an argument for pulling some of it earlier than Phase 4.

**Part of this landed in Phase 2, because not landing it was a defect.** Vanilla's `UpdatePortal`
already dims a portal's runes when the nearby player carries something non-teleportable — the channel
this section asks for has always existed. Once Phase 2 changed the *rule* without changing the
*indicator*, the glow started lying: carrying copper darkened every portal in the world, including
those whose destinations would accept it. An indicator that is wrong is worse than none, because
players trust it before they learn its limits. The glow now answers per-destination, sharing its
destination resolution with the travel gate so the two can never disagree.

What remains here is the louder half: the red flare, the rune curtain, and the HUD line.

Fix — and it improves the base experience too:

- Resolve the destination's mask when the player comes within ~4 m of an open portal, not at transit.
- On refusal: flare the runes red, drop a rune-curtain collider across the doorway, HUD line naming
  the item and the missing stave.
- You get told **before** you commit, which beats vanilla's silent stone wall.

Scope call: ship last, default off, treat as replaceable. Maintained mods already do preloading
well — depend on one rather than owning that code.

Cheap 80% of the *feeling*: a **destination thumbnail** in the panel, snapshotted the last time any
player stood at that portal and cached with the portal record. Phase 3 nicety, not a renderer.

---

## 8. Build order

| # | Phase | Ships | Standalone? |
|---|---|---|---|
| 1 | **Destination selector** ✅ | Portal registry, server sync, the one-way target on the portal ZDO, and the map selector with keyboard *and* gamepad nav. | Yes — and it's the must-have. |
| 2 | **Staves** ✅ | Six pieces, the mask, server recompute, the destination check, named refusals on entry. | Yes. The mod's reason to exist. |
| 3 | **Telling the player** ✅ | Build-mode feedback (§5) — range and the portal a rune reached. The blocked-cargo overlay on inventory icons. Clearance chips in the selector and the cargo filter. Thumbnails and favourites deferred as niceties. | Needs 1 + 2. |
| 4 | **Seamless transit** ✅ | Approach-time warning in words, and a trip that ends when loading does. Default off. Preloading and the rune curtain deliberately not built — see below. | Optional — cut without regret if it fights the game. |
| 5 | **Release readiness** | Localisation ✅. Masks verified over a real network, two machines. `LogNetworkSync` defaulted off, *after* that verification and not before. Thunderstore packaging ✅. | The last things between a working mod and a shippable one. |

Phase 2 shipped playable on the entry message alone, which is the *worse* half of §5's two layers —
you learn at the threshold instead of while packing. That is why Phase 3 is named for what it does
rather than called polish: the mod currently enforces two rules it barely explains. A stave planted
eleven metres from a portal does nothing and says nothing, and the only way to discover it is to haul
ore and be refused.

Phase 5 exists because "it works on my machine" is the one claim this project has repeatedly had to
withdraw. `LogNetworkSync` stays on until masks have been watched crossing a real network, and is
turned off in the same change that confirms they do.

### What phase 4 turned out to be

Most of it was a decision not to build something. §7's own scope call — depend on a maintained
preloading mod rather than own that code — held up: an attempt at dragging destination zones in early
with `ZoneSystem.PokeLocalZone` was written and thrown away, because Valheim unloads zones outside the
active area and the attempt was arguing with the game about something the game is right about.

What remained was worth having, and came from reading `Player.UpdateTeleport` rather than from the
plan. A distant trip is not slow because loading is slow; it is slow because the method will not even
*check* whether the destination is ready until eight seconds have passed. So a destination already in
memory now skips the screen entirely, and one that is not shows it for exactly as long as the zone
genuinely takes. Nothing is loaded early and `IsAreaReady` still decides what ready means.

The rune curtain was dropped too. Its purpose in §7 was to stop a player walking in when seamless
transit removes the refusal moment — but the refusal moment is still there, and now happens on
approach as well, so a collider would only remove the ability to walk in and be told why.

### What Phase 1 actually shipped

Built and verified in game: the server-swept registry with its clearance-mask field reserved,
`stave_pid` identity, one-way targets honoured by a `TeleportWorld.Teleport` prefix with vanilla
tag pairing intact as the fallback, the map-and-list selector with rebindable keyboard and gamepad
bindings, `ReaimPermission`, `HidePortalNames`, and the `stave_portals` / `stave_aim` /
`stave_net` commands.

Deferred from §5 rather than forgotten: favourites, which needs per-player state nothing else yet
requires, and the cargo filter, which had nothing to filter on until Phase 2 produced masks.
Clearance chips are Phase 3 by design.

**Not yet proven: the client half of the sync.** Single player is its own server, so
`AddInitialSynchronization` and the broadcast have never run. `stave_net` exists to settle it in
one command per machine — see §6.

---

## 9. Decisions

### Settled

| Question | Decision |
|---|---|
| Station or rewire? | **Rewire, selected on the map** (§5). A portal's destination belongs to the portal and applies to everyone; walk in to travel, interact to re-aim. Station is deferred to §13 and is not being built. |
| Independent per-tier flags, or a strict ladder (tier 3 requires 1 + 2)? | **Independent flags.** `StrictLadder` opts into requiring the lower staves first. |
| Anchor-and-radius, or bind each stave to one portal? | **Auto-bind each rune to the nearest portal in range** (`PortalBinding = Nearest`), no anchor and no manual binding UI. `AllInRadius` covers every portal at the site. |
| Does the source ever matter? | **Yes, under `MaterialFlow`** — `Receive` (destination only, the original rule), `Deliver` (source only), `Both` (either, and the shipped default). Two earlier drafts said *never, and there is no setting*; that is now wrong, and §10 records what the default gives up. |
| Own the destination list, or build on XPortal (GPLv3)? | **Own it.** Inspiration only, no copied code, MIT preserved. §11 spells out where the line sits. |
| Who may re-aim a portal? | **`ReaimPermission`, default `Anyone`**, with `GuardStonePermitted` / `Admin` — see §5. |
| How is a player warned *before* they commit? | **A blocked overlay on inventory icons near a portal**, plus R6's named message on entry — see §5. |
| Permanent staves, or an ongoing sink? | **Permanent, with no fuelled mode at all.** The gate is the boss kill, not upkeep — see §10. |

One portal per site is what makes the binding default reasonable, and that survives the move to
rewire: re-aiming reaches every destination from a single portal, so a site needs exactly one and
there is nothing to disambiguate. Binding also dissolves the overlapping-site question — nearest wins,
deterministically — and gives back the "loading dock" pattern (two portals at one location with
different clearance) that a site-wide radius cannot express.

The caveat is contention: if re-aiming fights push players into building hubs after all, `Nearest`
would charge a full stave set per portal in the hub. `AllInRadius` is the escape hatch, and that is
now its main justification rather than the large-base case it was kept for.

Dropping source enforcement outright, rather than shipping it off by default, is worth being precise
about — because the two are not equivalent, and the difference *is* the mod. Checking the source too
would mean an outpost with no staves could no longer send ore anywhere, since it has none of its
own to authorise the departure. That kills the one-way outpost in §1: ore would need staves at
both ends, the network would become symmetric, and "ore flows inward toward places you invested in"
would just become "build staves everywhere". A setting that can switch off the central mechanic is not a setting worth
having.

Explicit manual binding was rejected on cost: it needs a bind interaction, gamepad navigation for it,
a stored ZDOID per stave, and dangling-reference handling when either end is destroyed while the chunk
is unloaded. Auto-binding is recomputed from positions on the server's sweep, so it is self-healing
and stores nothing that can rot.

### Still open

Nothing structural, and the balance question that was open longest turned out to be misframed rather
than unanswered — §10 has it. What remains is one guess: the ten-metre `StaveRadius`, which decides
how tightly a site has to be built and wants play rather than argument.

---

## 10. The balance question

This began as the design's main worry. The cost curve looked like the only brake, and trophies are
farmable: on a server that has killed Yagluth, a determined group can build staves at every site
they own in an afternoon of boss re-summons — at which point, the fear went, Stavebound quietly becomes
an unrestricted-portals mod with extra steps.

**A fuelled mode was the other available answer, and it has been rejected** (R7). The gate is meant
to be the boss kill, not upkeep: once you have beaten a biome you have moved on to the next one, and
the trips back to earlier zones for resources are exactly the drudgery this mod exists to remove.
Charging rent on a network you already earned would put that friction back in the wrong place.

### Settled: the risk was the wrong way round

The worry above assumed permissiveness was the failure. It is not — it is the point. A stave costs
**the boss's head plus some of what that biome gives you**, and what it buys is the right to move that
biome's goods more freely. A group that has killed Yagluth and can now shift black metal around their
network has not broken the rule; they have finished paying for it.

That reframing also names the real brake, which was never the cost curve. It is **R3**. Clearance is
read at the destination, so every site you want to receive at needs a rune *standing there* — which
means going there, on foot or by boat, carrying the pieces. That cost is per-site and cannot be farmed
by re-summoning anything. Trophies being farmable makes the tenth rune cheaper than the first; it does
nothing about the tenth *journey*, and the journey is what the mod is actually charging for.

So the numbers in §4 stand: a trophy and ten of the metal, which reads as a real errand without
pretending to be a wall. If play shows otherwise the lever is the metal component, and it is a config
line rather than a design change.

### What the `Both` default costs that brake

The argument above rests on R3 as it was: clearance read at the destination, so *every* site you want
to receive at needs its own journey. `MaterialFlow = Both` weakens that, and the weakening is worth
stating plainly rather than discovering in play.

Under `Both`, one end suffices. A single fully-staved capital therefore makes the whole network
permeable **in two hops** — outpost A to the capital, capital to outpost B — where under `Receive` the
second leg is refused until B has a stave of its own. Direct A-to-B still needs one of them to have
paid, so the rule is not nothing; but the per-site journey stops being per-site once a hub exists.

That is a real cost and it buys real things: no forgotten stave stranding a load at a portal you
built last week, and metals moving *out* to a frontier that has not been kitted out yet. The lever if
it proves too loose is `Receive`, which restores the original rule exactly and needs no code — which
is the reason this is a setting rather than a rewrite.

**This has not been played.** Both the two-hop effect and whether `Both` or `Receive` is the better
shipped default are open questions that want real sessions — see TESTING.md.

---

## 11. Licensing

**Recommendation: MIT for the code**, with two carve-outs below.

Why MIT: it's the de-facto default across the Valheim/BepInEx ecosystem, it imposes nothing on
users who bundle the mod into modpacks, and — the real reason — Valheim mods get abandoned
constantly. A permissive licence means that when you stop updating this, someone can legally fork
and keep it alive for the next game patch instead of doing an unlicensed reupload. Apache-2.0 is a
fine alternative if you want an explicit patent grant and a formal attribution requirement; it's
strictly more paperwork for the same practical outcome here.

Two things constrain the choice:

1. **If you fork XPortal, you must ship GPLv3.** XPortal is GPL-3.0, so any derivative is too, and
   that relicenses your whole assembly. This got harder once §5 settled on rewire, because rewire is
   precisely what XPortal already implements — the overlap is no longer theoretical. Writing the
   registry, the one-way target and the map selector ourselves is what keeps the licence choice in
   our hands, and it is a few days of work against permanently binding the assembly. You can still
   *read* XPortal for approach; copying code is what triggers it.

   **Settled: own it. Inspiration only.** Copyright protects expression, not ideas — it does not
   reach "any idea, procedure, process, system, method of operation, concept, principle, or
   discovery" (17 U.S.C. §102(b)). GPL-3.0 is a copyright licence, so if nothing is copied it never
   attaches. In practice:

   | Free to take | Not free to take |
   |---|---|
   | The mechanic — a portal stores a target and syncs it to everyone | Source, whole or partial |
   | The architecture — server-authoritative registry, client cache resynced on join | Copy-then-rename, which is still a derivative |
   | That a problem exists and roughly how it is solved | Paraphrase close enough to be recognisably the same code |
   | Facts about the **game's** API — that `ZDOMan.GetPortals()` exists is a fact about Iron Gate's code, not XPortal's expression | |

   The working rule: read it to understand *what* and *why*, then close the file and write ours from
   this document. The failure mode is not reading — it is having their implementation open in the
   other window while typing the equivalent function, which drifts into transcription without anyone
   deciding to.

   Use our own ZDO keys (`stave_*`). The legal case against reusing theirs is weak — short
   identifiers are barely copyrightable — but the functional one is decisive: two mods writing the
   same key on the same world would corrupt each other.

   We start from a good position here. This document was written from the problem, not reverse
   engineered from their code, so the spec we implement against already exists independently — which
   is most of what a formal clean-room process buys, without the ceremony.

   Credit AnyPortal and XPortal as inspiration in the README at ship time. Not required if nothing is
   copied; good manners, and free.

   *(Not legal advice — this is the standard understanding in the modding ecosystem.)*
2. **A code licence does not cover art or game assets.** Keep those separate:
   - **Never commit Valheim's DLLs, publicised assemblies, or ripped meshes/textures** to the repo.
     They're Iron Gate's, redistribution isn't yours to grant, and it's the fastest way to get a
     repo taken down. Reference game assemblies from a local install path via an env var and
     `.gitignore` them.
   - Prefer **cloning existing in-game prefabs at runtime** (a rune stone, a standing stone) for the
     staves over shipping custom models. Cheaper, always matches the art style, and there
     is nothing to license.
   - If you do ship original models/icons in an asset bundle, license the art separately in the
     README — CC BY 4.0 is the usual pick — because MIT's "software" wording maps badly onto art.

Practical setup: `LICENSE` (MIT, your name), plus a short `NOTICE` or README section reading roughly
*"Code: MIT. Art assets: <licence>. Valheim and its assets are property of Iron Gate Studio; no game
files are redistributed."* Then set the Nexus permission fields and the Thunderstore manifest to
match — a repo that says MIT next to a mod page that says "do not reupload" is the single most
common licensing mistake in this ecosystem.

---

## 12. Game API — verified and still unverified

Everything here originally came from mod sources and recollection. The first half has since been read
off the shipped assemblies; the second half still hasn't.

### Verified

Read from `assembly_valheim.dll` with Mono.Cecil — originally at game build **5.4.23.2+3** (Aug 2026),
and **re-read in full against Valheim 1.0 (Sept 2026)**. Anything below is what the game actually
contains, not what the spec assumed.

#### What Valheim 1.0 changed

Two breaks, both found by the compiler rather than in play, and both in code that runs constantly:

| Was | Is now |
|---|---|
| `InventoryGrid.Element` (nested), with an `m_pos` field | Top-level **`InventoryElement`**, with a public **`Position`** property |
| `ZDOMan.GetPortals()` returning `List<ZDO>` | Returns `Dictionary<ZoneSystem.SectorIndex, List<ZDO>>`; **`GetPortalList()`** has the old shape |

`GetPortalList()` is not a drop-in twin of the old `GetPortals()`: it allocates a fresh flattened list
per call rather than exposing the live one. That removes the old hazard of mutating the game's own
collection, at the cost of a small allocation on each sweep.

Everything else the mod touches survived unchanged, and this was checked rather than assumed — every
Harmony target (`UpdateGui`, `Game.OnDestroy`, `Minimap.Update`, `ObjectDB.Awake`,
`UpdatePlacementGhost`, `UpdatePortal`, `HaveTarget`, `TargetFound`, `UpdateTeleport`) and every
injected private field (`m_nview`, `m_inventory`, `m_elements`, `m_placementGhost`, `m_teleporting`,
`m_teleportTargetPos`, `m_teleportTimer`, `m_distantTeleport`). The string-named patches and the
`___field` injections are the ones worth re-checking on any future update: neither is compile-checked,
so both fail at patch time rather than build time.

The IL that §7's seamless transit depends on is also intact — the eight-second gate, the
`m_distantTeleport` test and the `IsAreaReady` call still appear in that order, with the two-second
pause and fifteen-second timeout unchanged.

**Jotunn 2.30.0 is required** from here on; 2.29.2 predates 1.0. One upstream limitation comes with it,
and it is visible in game: Jotunn has not ported piece *categories* to 1.0's overhauled system, so the
six staves appear in the hammer menu without sitting under `Misc` as `PieceConfig.Category` asks. Not
ours to fix.

**`TeleportWorld`** — fields `m_activationRange`, `m_exitDistance`, `m_allowAllItems`, `m_proximityRoot`;
methods `GetHoverText()`, `GetHoverName()`, `Interact(Humanoid, bool, bool)`, `UseItem(Humanoid, ItemData)`,
`Teleport(Player)`, `GetText()` / `SetText(string)` (it is a `TextReceiver`), and private
`HaveTarget()`, `TargetFound()`, `UpdatePortal()`, `SetConnectedPortal(ZDOID)`, `RPC_SetConnected`,
`GetTagSignature(out string tagRaw, out string authorId)`.

- **`GetConnectedPortal` does not exist.** It was never a method; earlier drafts invented it.
- `Teleport(Player)` is the travel entry point, and it is where the checks live: `GlobalKeys.NoPortals`,
  then `GlobalKeys.NoBossPortals`, then — unless the portal's own `m_allowAllItems` is set —
  `Humanoid.IsTeleportable()`, refusing with `Character.Message(MessageHud.MessageType.Center, "$msg_noteleport")`.
  That last branch is the one **R6** replaces with a named reason.
- `Interact` gates on `PrivateArea.CheckAccess(transform.position, 0f, flash: true, wardCheck: false)`
  and then opens `TextInput.RequestText(this, "$piece_portal_tag", 10)`. That `RequestText` call is
  precisely what the §5 map selector replaces.

**The destination is a ZDO *connection*, not a tag lookup.** `Teleport` resolves it as
`zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal)` → `ZDOMan.GetZDO(...)` → position/rotation →
`Character.TeleportTo(pos, rot, distantTeleport: true)`. `TargetFound()` calls `ZDOMan.RequestZDO(id)`
when the destination ZDO isn't loaded locally — the precedent for reaching a portal kilometres away.

**`Game.ConnectPortals()` will tear down anything we write into that connection.** It runs on the
server only (`Game.Start` starts `ConnectPortalsCoroutine` behind `ZNet.instance.IsServer()`) and
repeats **every 5 seconds**. Two passes:

1. For every portal with a connection: if the target ZDO is gone, **or the target's `ZDOVars.s_tag`
   differs from this portal's**, clear the connection.
2. For every unconnected portal: find a random unconnected portal with the same tag and connect
   **both ends** to each other.

So the vanilla connection is by construction symmetric and tag-derived, and a one-way target written
into it would survive at most five seconds. §6 records the consequence: our target lives in our own
ZDO key, `Teleport` is patched to prefer it, and vanilla's pass is left running untouched — which is
what makes "vanilla tag pairing as the fallback" in §5 fall out for free rather than needing code.

`ZDOMan.ConvertPortals()` migrates pre-connection saves off a legacy `ZDOVars.s_toRemoveTarget` ZDO
key. That key is dead; don't reuse the name or the pattern.

**Teleportability** — `Player.IsTeleportable()` **does not exist**. It is `Humanoid.IsTeleportable()`,
which forwards to `Inventory.IsTeleportable()`, which returns true immediately if
`GlobalKeys.TeleportAll` is set and otherwise scans `m_inventory` for
`ItemDrop.ItemData.m_shared.m_teleportable == false`. `Inventory.GetAllItems()` and
`Player.TeleportTo(Vector3, Quaternion, bool)` both exist as assumed.

**`ZDOMan.GetPortals()`** — public instance method on `ZDOMan.instance`, returning `List<ZDO>`. It
returns the **live `m_portalObjects` list, not a copy**; read it, never mutate it. Server-side
population, consistent with the server-authoritative registry in §6.

**ZDO custom fields** — `Set(string, ZDOID)` / `GetZDOID(string)` / `RemoveZDOID(string)`, with
hash-pair overloads via the cacheable `ZDO.GetHashZDOID(string)`; ints via `Set(string, int)` /
`GetInt(string, int)`. String keys hash through `StringExtensionMethods.GetStableHashCode` in
`assembly_utils`. Vanilla caches its own hashes as statics on `ZDOVars` (`s_tag`, `s_tagauthor`, …);
ours do the same with `stave_` names.

**`PrivateArea.CheckAccess(Vector3 point, float radius, bool flash, bool wardCheck)`** — public
static, and the call behind `ReaimPermission = GuardStonePermitted`. Vanilla already gates portal
interaction on it, so that setting is mostly a matter of not weakening what is already there.

**A ZDOID is not a persistent reference.** Every ZDO in the world is renumbered on every save and
load. Measured across one logout to the main menu and back:

| Portal | Before | After |
|---|---|---|
| built earlier, already saved | `1:20372` | `1:20375` |
| built earlier, already saved | `1:27015` | `1:27018` |
| built during that session | `2261713014:42343` | `1:32429` |

So both halves move: a ZDO created in a session carries that session's id and comes back under a
different one, and even long-persisted ZDOs have their numeric id shifted. A stored ZDOID therefore
points at nothing after a relog — or, worse, at whatever object inherited its number.

This is why vanilla persists portal connections as `ZDOConnectionHashData` in
`ZDOExtraData.s_saveConnections` and rebuilds the real ids after load, and why `ZDOMan.ConvertPortals`
migrates old saves off the ZDOID-valued `ZDOVars.s_toRemoveTarget` key. **Anything of ours that must
outlive a session refers to a portal by its own `stave_pid`**, a 64-bit id the server mints once
and the registry resolves to a live ZDOID on demand. ZDOIDs are still the right handle *within* a
session — they are how you reach the object — but they may never be written down.

**Publicised assemblies are a compile-time fiction, and this game build enforces that.** Jotunn's
prebuild publicises the game assemblies and every reference resolves against those, so
`portal.m_nview` compiles without complaint. At runtime the *real* `assembly_valheim.dll` is loaded,
the field is private again, and this build's Mono raises the access check:

```
FieldAccessException: Field `TeleportWorld:m_nview' is inaccessible from method
`Stavebound.Patches.TeleportWorldPatches:HaveOurTarget (TeleportWorld,bool&)'
```

It throws on every call, and a field read from a per-frame path such as `GetHoverText` produces five
figures of log spam in a couple of minutes. Nothing warns at build time, so the rule has to be held
by hand: **never read or write a private game member directly.** Patch methods take Harmony's
`___fieldName` injected parameter; everything else goes through a cached
`AccessTools.FieldRefAccess`, which is emitted once and costs about what the field access would have.
Public members — and most of what we need is public — are fine as they are.

Two things the game has grown that the spec didn't know about:

- **`TeleportWorld.m_allowAllItems`** — a per-prefab flag that skips the teleportable check entirely.
  Our gate has to respect it or we will refuse cargo at a portal the base game lets through.
- **`ZDOVars.s_tagauthor`** — vanilla now records who set a portal's tag. The `Builder` value dropped
  from `ReaimPermission` in §5 was dropped because nothing recorded the placer; this records the
  *tagger*, which is close but not the same thing. Noted, not reopened.

### Still unverified

These need the game running or the asset database loaded, so the assemblies can't answer them:

- Boss trophy prefab names, especially **The Queen** and **Fader** — verify in `ObjectDB`.
- The authoritative non-teleportable item list — from the `ObjectDB` scan at runtime, not from a wiki
  and not from this document.
- The **in-game guard stone effect** reused for the build-mode range and connection indicators in §5 —
  the prefab name, the component that drives it, and whether its radius can be driven at runtime.
- The **inventory slot UI** for the blocked-cargo overlay in §5: how `InventoryGui` / the inventory
  grid builds and refreshes slot elements, and where a child image can be attached so it survives a
  refresh. Vanilla already draws quality stars and durability bars on those elements, so the hook
  exists.
- Which vanilla prefabs ship with `m_allowAllItems` set.
- **Plugin GUIDs of the conflicting mods** in §6. `Compat/ConflictDetector.cs` currently holds only
  the two confirmed from source (Valheim Plus `org.bepinex.plugins.valheim_plus`, XPortal
  `SpikeHimself.XPortal`); Advanced Portals, Progression Portals, Gate of Ore-thority and AnyPortal
  are covered only by a `portal`/`teleport` keyword heuristic until someone reads their GUIDs off
  the actual plugins. A wrong GUID in that list fails silently, which is why guesses stay out of it.

Also worth reading directly: [XPortal's source](https://github.com/SpikeHimself/XPortal) (GPLv3 —
read for approach, don't copy code unless you're licensing GPLv3), which uses ZDO keys
`XPortal_TargetId` / `XPortal_PreviousId` and a client `KnownPortalsManager` resynced from the
server.

---

## 13. Future ideas — noted, not planned

Things deliberately set aside. Nothing here is scheduled; they are recorded so the reasoning is not
lost and so nobody re-derives them from scratch.

### Discovered portals only

A setting that made a portal selectable only once you had stood at it, so the map could not hand you
somewhere you had never been. Cut before it was built: this is a mod for playing with friends, and the
shared world is the point. Someone joining a server and being able to reach the base they have been
invited to is the behaviour you want, not a leak. It also needs per-player state nothing else requires.

Worth reviving only if a public server ever wants portal discovery as a progression gate.

### Station mode — per-player, per-trip destinations

Handy portals' model. Interact, pick a destination, travel immediately; the choice is yours alone and
nothing is written to the portal.

It gives up the thing rewire is built around — a portal that means the same to everyone — but it has
two real advantages worth remembering if the shared-target model turns out to chafe:

- **No contention.** No two players ever fight over one portal's destination, and re-aiming cannot
  strand somebody mid-haul.
- **The clearance check lands in the right place.** Each trip is a fresh question, so choosing a
  destination and being told "no" are the same moment — no walking into a wall, no need for
  approach-time warnings to carry the refusal.

It would slot in as a `SelectionMode` alongside rewire rather than replacing it. The cost is a second
travel path to maintain and test, which is why it is not being built now.
