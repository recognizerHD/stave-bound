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
- [ ] The `ObjectDB` tier map has only been read on game 0.221.12. A game update adding a blocked item
      should surface as an "unclassified" warning naming the prefab, and that path has never fired for
      real
- [ ] Conflict detection warns by GUID and has never been run against an actually-installed
      conflicting mod
- [ ] Seamless transit has been played, but not with a client whose destination is unloaded **on a
      server** — the case it exists for. The single-machine case is covered

---

## Confirmed

Kept as a record of what the tests were, so a regression has something to be measured against.

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
