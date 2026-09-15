# What has not been tested

This file exists to shrink. Anything listed here is built and believed to work and **not confirmed**;
when something is confirmed it moves to the record at the bottom and stops being a caveat anywhere
else in the repo. It is not packaged in the Thunderstore zip.

Contributors: do not mark anything here done from a code reading. Every line got here because reading
the code already convinced someone, and that turned out not to be enough.

---

## 1. Diagnosed — a traveller's body left at the departure portal

**Diagnosed as the game's, not fixed.** When another player walks through a portal,
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

**Evidence, 2026-09-15 — `STALE COPY`.** A two-player run with the leftover body in view. On the
watcher's machine the traveller was drawn and held at **(391, −429)**, beside the watcher, while the
server's player list put them at **(2544, 379), 2,300 m away**; their data revision sat at 23790,
unchanged across the three-second watch. The watcher's own entry was fully consistent.

What that settles: **the traveller's game did report the teleport** — the server knew where they had
gone — so the teleport path, the one place this could have been Stavebound, is cleared. What went
wrong is entirely on the watcher's side: their copy of the traveller's ZDO froze at the portal once the
server stopped streaming it, and the game never culled the body. That is the game's area streaming.

**A fix is possible client-side, using only the game's own machinery**, both halves read off the 1.0
assembly:

- `ZDOMan.RequestZDO` routes to the server's `RPC_RequestZDO`, which calls `ForceSendZDO` for that
  peer — it sends the ZDO **regardless of whether it is in the requester's area**. The portal code
  already relies on this to fetch distant portals.
- `ZNetScene.CreateDestroyObjects` lists ZDOs near the reference position with `FindSectorObjects` and
  passes them to `RemoveObjects`, which destroys any instance not among them. A copy refreshed with the
  traveller's real, distant position falls out of that list, and the body is removed.

Proposed: every couple of seconds, per other player — request a fresh copy when the server's list puts
them well away from the held copy; and, since that list is empty for players hiding their map position,
also when a drawn player's data has not changed for several seconds, which costs nothing for someone
merely standing still nearby. Rate-limited per player, behind a setting. **Not built.** The one untested
step is that the cull follows promptly once the fresh copy lands.

The mod-disabled test below would still make "not ours" certain rather than strongly evidenced.

**Collecting the evidence — `stave_players`.** Needs the F5 console switched on (in 1.0's settings, or
the `-console` launch option) but **not** `devcommands`: it is registered as neither a cheat nor hidden
behind dev commands. It watches for three seconds, then prints each player three ways — where they are
drawn, where this machine's data says, and where the server's player list says — with a verdict line.

- [ ] The traveller ticks **"Visible to other players"** on their map first, or the server column is empty
- [ ] Traveller walks through a portal. While the body is still visible, the **watcher** runs
      `stave_players`; the **traveller** runs it too
- [ ] Both copy the output — it is also in each machine's `BepInEx/LogOutput.log`

What the verdicts mean:

| Verdict | Meaning | Whose |
|---|---|---|
| `STALE COPY` on the watcher | The watcher stopped being sent the traveller's position after they left its area, and never cleaned up the old body | The game's streaming; fixable client-side |
| `BODY NOT MOVED` on the watcher | The position arrived; the body was never moved to it | Almost certainly the game's; fixable by snapping |
| `YOUR OWN data is ...m from where the server says` on the traveller | The traveller's own game never reported the move | **Potentially ours** — the teleport path |

The deciding test, which settles whose it is regardless:

- [ ] Disable Stavebound for **every** player **and** the server (the mod requires all of them to match,
      so a half-modded setup will not connect), pair two portals by name, and watch someone go through.
      Body left behind → the game's. Gone → ours
- [ ] Note **when** the body disappears on its own, if it ever does, and whether seamless transit being
      on for the traveller makes any difference

## 2. Balance — wants sessions, not checklists

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

## 3. Standing gaps

Smaller, older, and none of them blocking.

- [ ] Gamepad navigation of the selector has been exercised far less than keyboard — and not at all
      since the panel was rebuilt for the mouse. Navigation is off on every new control precisely so the
      pad is unaffected; that is the claim to check
- [ ] The wheel on a **trackpad**, where one swipe should step a handful of rows rather than racing to
      the end of the list
- [ ] Conflict detection warns by GUID and has never been run against an actually-installed
      conflicting mod
- [ ] Seamless transit has been played, but not with a client whose destination is unloaded **on a
      server** — the case it exists for. The single-machine case is covered

## Confirmed

Kept as a record of what the tests were, so a regression has something to be measured against.

### The mouse-driven selector — passed

The panel used to be one rich-text `Text`, which could neither right-align part of a line nor tell
which line was clicked. It is now separate elements, and every behaviour below was confirmed in play:

- Rows with clearance chips right-aligned on the same line as the name
- **Keys browse, clicks choose**: arrow keys highlight and P picks; clicking a row or a dropdown entry
  picks at once; clicking a portal's pin on the map picks it and closes the map, and clicking empty map
  does nothing
- The dropdown, following the highlight and picking on selection
- The footer's key hints as working buttons
- The mouse wheel scrolling the list without zooming the map, and still zooming it elsewhere
- Hovering a row panning the map to it, and returning to the highlight on leaving the list
- The portal nearest the player's **bed** in light blue, in the list and on its pin; every other
  destination's pin orange, distinct from pins the player placed
- Each of those switchable under `8 - Selector`

Two regressions worth remembering, both reachable before this work: with the cargo filter emptying the
list, the confirm key indexed an empty list and the arrow keys divided by zero. And closing the
selector checked `m_mapLarge.activeSelf`, which `SetMapMode` never changes, so a pick could leave the
map open — it checks `m_mode` now, as `Update` always did.

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
