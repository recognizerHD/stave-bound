# What has not been tested

This file exists to shrink. Anything listed here is built and believed to work and **not confirmed**;
when something is confirmed it moves to the record at the bottom and stops being a caveat anywhere
else in the repo. It is not packaged in the Thunderstore zip.

Contributors: do not mark anything here done from a code reading. Every line got here because reading
the code already convinced someone, and that turned out not to be enough.

---

## 1. Balance — wants sessions, not checklists

Open questions that only real play answers. Nothing here is a bug, and nothing here blocks a release
— it decides what the shipped defaults should be.

- [ ] **Is `Both` the right default?** It retires the one-way outpost as the law. `Receive` restores
      it exactly and costs nothing but a config line. See DESIGN.md §10
- [ ] **The two-hop hub.** Under `Both`, one fully-staved capital makes a whole network permeable via
      A → capital → B. Does that feel earned, or does it hollow out the per-site journey the design
      charges for?
- [ ] **The §4 costs** — a trophy and ten of the metal — are placeholders and have never been tuned.
      The lever is the metal component, and it is a config line rather than a design change
- [ ] **Does `Deliver` have an audience,** or is it a symmetry nobody plays?

## 2. Standing gaps

Smaller, older, and none of them blocking.

- [ ] Gamepad navigation of the selector has been exercised far less than keyboard
- [ ] Conflict detection warns by GUID and has never been run against an actually-installed
      conflicting mod
- [ ] Seamless transit has been played, but not with a client whose destination is unloaded **on a
      server** — the case it exists for. The single-machine case is covered


## Confirmed

Kept as a record of what the tests were, so a regression has something to be measured against.

### Valheim 1.0 — passed

The game's 1.0 release moved two things out from under the mod: `InventoryGrid.Element` became a
top-level `InventoryElement` with a `Position` property in place of `m_pos`, and `ZDOMan.GetPortals()`
started returning a dictionary bucketed by sector, with `GetPortalList()` carrying the old shape. Both
are in code that runs constantly. See DESIGN.md §12 for the full re-verification.

Confirmed in game on Jotunn 2.30.0: pieces register, the sweeps mint pids and re-aim
portals, `SiteSweep` writes masks ("now Elder", then "Elder + Bonemass"), the selector opens, and the
cargo overlay marks the right stacks. No Harmony patch failed to apply.

The tier map grew from 26 blocked items across 1084 in `ObjectDB` to **28 across 1520**, which is the
Deep North arriving.

### The 1.0 build menu — passed

1.0 rebuilt the build menu and Jotunn 2.30.0 has not ported categories to it, so `PieceConfig.Category`
is ignored and the staves landed nowhere but "Show All". The mod now sets the piece's own fields on
`PieceManager.OnPiecesRegistered`, after Jotunn has finished applying the config it would otherwise
overwrite them with.

Confirmed: the six staves sit under **Transportation** and carry the up-arrow.

The trap worth remembering is that 1.0 has **two** category systems — the old `Piece.m_category`
(`PieceCategory`, nine entries, no transport) and the new `Piece.m_usage` (`UsageTagFlags`, twenty,
including `Transport`) which is the one the player sees. Checking the first and concluding there was
no transport category cost a round trip here. See DESIGN.md §12.

### Sealed resources — passed

Bloodgold and Petrified Tissue are `Clearance.Sealed`, a tier no stave grants. Confirmed: a normal
portal refuses them and says only a stone portal will carry them, an Ashen Stave does not change that,
and the stone portal is unaffected — `m_allowAllItems` bypasses the gate entirely.

### Clearance across a real network — passed

The largest untested claim in the mod, and the one that gated the release. The registry sync had been
proven on two machines *before* masks existed, so until this ran, no client had ever received a
non-zero clearance mask.

Confirmed working on a networked server: clients receive masks for portals they have never visited,
are refused and permitted by masks their own machine never computed, and see staves built elsewhere
appear within the sweep interval.

`LogNetworkSync` was flipped to default off on the strength of this. Turn it back on before reporting
anything about portals disagreeing between machines — it narrates every sweep, broadcast, join and
receive, and it is the first thing anyone will ask for.

### `MaterialFlow` — passed

Built, reasoned about and shipped in the same change, which is exactly the situation this file exists
to stop. Since confirmed in play.

The rule, for anything that needs re-checking later: a trip is allowed if **either end vouches for the
tier**, and which ends may vouch is the setting — `Receive` the destination, `Deliver` the portal you
leave, `Both` either. The three refusals that carry the whole truth table are `Receive` staved→bare,
`Deliver` bare→staved, and `Both` between two bare portals. A gate that has gone permissive shows up
nowhere else.

All five surfaces read one function, `ClearanceGate.EffectiveMask`, so any disagreement between the
rune glow, the approach warning, the inventory marks, the selector verdict and what actually happens
on walking in is a real bug rather than a cosmetic one.

---

## How to test a build from scratch

The full pass, for when something big changes. Console commands go in Valheim's F5 console, not a
shell. Launch the **dev profile**, make a **throwaway world** (r2modman does not isolate saves), and
`devcommands`.

**1. Did the patches apply?** Before judging anything in game, read the log. A `___field` injection
that no longer matches fails at patch time, logs, and leaves everything else working — so it is
invisible in play.

```
grep -iE "harmony|exception|failed to|error" "<profile>/BepInEx/LogOutput.log"
```

Expect `Loading [Stavebound <version>]` and six `Registered piece stave_*` lines.

**2. Pieces registered.** Open the hammer, find the six staves. If they are missing entirely, the
clone source is the suspect: `stave_prefabs corestand`.

**3. The sweeps.** Spawn two portals (`spawn portal_wood 1`, twice), name both the same so they pair,
then `stave_portals`. Both should be listed. An empty list means the registry is not seeing the
world. Single player is enough — you are the server.

**4. Stave binding.** `spawn TrophyTheElder 1`, `spawn Copper 10`, `spawn Stone 20`. Build the Elder's
Stave within 10 m of a portal, wait ten seconds for the sweep, and run `stave_portals` again — that
portal should show a non-zero clearance. This covers a different code path from step 3.

**5. The cargo overlay.** `spawn Iron 5`, `spawn CopperOre 5`. Stand **at a portal that points
somewhere** — the patch does nothing otherwise — and open your inventory. With only the Elder's Stave
built, copper ore should be unmarked and iron marked. Testing with one of each matters: a wrong slot
index marks the wrong stack rather than crashing, and a single item cannot tell you which happened.

**6. The gate agrees.** Walk in carrying the iron: a refusal naming Bonemass's Stave. Drop it, carry
the copper ore through: you travel. Overlay and gate both read `ClearanceGate.EffectiveMask`, so they
must agree.

**7. The item list.** `stave_items`. Anything the mod does not recognise is held to the highest tier
and logged by name; those names want adding to the right `*Items` config list.
