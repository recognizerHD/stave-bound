# What has not been tested

This file exists to shrink. Anything listed here is built and believed to work and **not confirmed**;
when something is confirmed it moves to the record at the bottom and stops being a caveat anywhere
else in the repo. It is not packaged in the Thunderstore zip.

Contributors: do not mark anything here done from a code reading. Every line got here because reading
the code already convinced someone, and that turned out not to be enough.

---

## 1. The mouse-driven selector — built, not run

The panel was one rich-text `Text`, which could neither right-align part of a line nor tell which line
was clicked. It is now separate elements: a dropdown, one clickable row per destination with its chips
right-aligned, and real buttons for the footer. Built against 1.0 and Jotunn 2.30.0; never opened.

- [ ] The panel draws at all, anchored to the left edge, with nothing overlapping
- [ ] **Chips sit right-aligned on the same line** as each name; a long name is cut short rather than
      running into them
- [ ] **Clicking a row highlights it** and pans the map to it — and does **not** re-aim
- [ ] **The dropdown** lists every destination in the same order as the rows, follows the highlight as
      the arrow keys move it, and picking from it moves the highlight
- [ ] With the dropdown **open**, the arrow keys move within its list and do *not* also move the
      highlight behind it; Escape folds the list rather than closing the selector
- [ ] **Each footer button** does what its key does: previous, next, sort, filter, confirm, cancel
- [ ] **The keys still work** after clicking — a clicked button must not keep focus and swallow P, or
      turn the arrow keys into UI navigation
- [ ] **A gamepad** still drives the whole thing, unchanged
- [ ] **The mouse wheel over the list** moves the highlight one row per notch — up for a wheel rolled
      away — and stops at either end rather than wrapping. **The map does not zoom** at the same time
- [ ] **The wheel over the map**, away from the panel, still zooms the map exactly as vanilla
- [ ] With the dropdown open, the wheel scrolls its list and the map does not zoom
- [ ] On a trackpad, one swipe steps a handful of rows rather than racing to the end
- [ ] **Hovering a row pans the map to it** without moving the highlight; sliding across several rows
      follows the pointer without the map bouncing back between them; **leaving the list returns the
      map to the highlighted destination**
- [ ] **Clicking the map still highlights the nearest destination.** It now reads the pointer through
      the game's own input rather than Unity's legacy one, which 1.0 moved away from — so this is both a
      regression check and possibly the first time it has worked on 1.0 at all
- [ ] Turn the cargo filter on while carrying something nothing accepts: the empty message shows,
      previous/next/confirm grey out, and **pressing confirm does nothing** rather than throwing. That
      last one was a real crash on the keys alone before this change, and so was stepping with the
      arrows — both would index or divide by an empty list

## 2. Suspected bug — a traveller's body left at the departure portal

**Not fixed, not diagnosed; flagged to look into later.** When another player walks through a portal,
an observer keeps seeing their body standing at the departure portal. The traveller really has gone —
they are on the other side — and the leftover body **keeps animating whatever they do there** (emotes,
actions) while never changing position. It disappears once the observer teleports themselves.

What is known so far:

- **The animation still updating proves less than it seems.** An earlier note here said it ruled out
  the obvious theory; it does not. `ZSyncAnimation.SetTrigger` sends emotes and actions as an RPC to
  every peer regardless of distance, while position travels in the player's ZDO, which the server
  only streams to peers nearby. So a body that keeps emoting is entirely consistent with the observer's
  copy of that ZDO having gone stale.
- **Leading theory:** the traveller jumps out of the observer's area, the server stops sending the
  observer that ZDO, and the observer's last copy still places the player at the portal — so the game
  never culls the instance, and broadcast animations keep playing on it. Teleporting away moves the
  observer's own area, which is why that clears it.
- Position syncs through `ZSyncTransform` and ZDO streaming, animation through `ZSyncAnimation`.
  **The mod touches none of them.**
- Both places Stavebound is in the teleport path were checked against the 1.0 assembly and match
  vanilla: the `TeleportWorld.Teleport` prefix transcribes 1.0's method call for call, and the flag
  seamless transit clears is read elsewhere only by the traveller's own loading screen.

So the best guess is a Valheim 1.0 bug — but that is a code reading, which is exactly what this file
says not to trust. The deciding test:

- [ ] Disable Stavebound for **every** player **and** the server (the mod requires all of them to match,
      so a half-modded setup will not connect), pair two portals by name, and watch someone go through.
      Body left behind → the game's. Gone → ours
- [ ] Note **when** the body disappears on its own, if it ever does, and whether seamless transit being
      on for the traveller makes any difference

## 3. Balance — wants sessions, not checklists

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

## 4. Standing gaps

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
