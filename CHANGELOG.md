# Changelog

## 0.9.1 — 2026-09-10

**Valheim 1.0 compatibility. 0.9.0 does not run on 1.0 — update.**

Two things the game moved out from under the mod. The inventory grid's slot type
was promoted out of `InventoryGrid` and its position field became a property,
which the cargo overlay reads. And portal ZDOs are now bucketed by sector, so the
call the server sweeps use to list every portal returns a dictionary rather than
a list — there is a new one with the old shape, and the sweeps use it.

**Jotunn 2.30.0 is now required**, since 2.29.2 predates 1.0.

One known rough edge, and it is upstream rather than here: Jotunn has not yet
ported piece categories to 1.0's rebuilt build menu, so the six staves appear in
the hammer but are not filed under Misc. They are still buildable.

Everything else came through 1.0 unchanged — every patch target, every private
field read, and the teleport timing seamless transit depends on were all checked
against the new assembly rather than assumed.

## 0.9.0 — 2026-09-06

First release. Both features have been played in single player and confirmed on a
real network. Numbered 0.9 rather than 1.0 because the costs have not been tuned
over a long game, not because anything is known to be wrong.

**Any-portal travel.** Interact with a portal to pick any portal in the world off
the map. The choice belongs to the portal and applies to everyone until someone
re-aims it. Pointers are one-way: aiming a portal at your base does not make the
return trip. Vanilla tag pairing still works untouched on any portal nobody has
re-aimed.

**Destination clearance.** Six staves, each bought with a boss trophy and a
little of the metal it unlocks. Stand one near a portal and that portal accepts
those metals — and refuses the rest by name: *"Iron cannot enter "Copper Mine" —
no Bonemass's Stave there."* Only the destination is ever checked, so an
outpost with no staves can send ore to your base forever and never receive
any.

**Knowing before you commit.** The portal's runes go dark when it will refuse what
you are carrying. Inventory slots mark the stacks that cannot make *this* trip
while you are near a portal, rather than the ones that can never teleport at all.
Walking up gets you the reason in words. Holding a stave shows its range and a
beam to the portal it would bind to.

**Optional seamless transit.** Off by default. A destination already in memory
skips the loading screen; one that is not shows it for as long as loading
actually takes, rather than for vanilla's fixed eight seconds.

Five console commands for looking at what the mod believes: `stave_portals`,
`stave_aim`, `stave_net`, `stave_items`, and the prefab tools
`stave_prefabs` / `stave_inspect` / `stave_preview`.

**Which end pays.** `MaterialFlow` decides whether a site's staves count for
arriving, departing, or both. `Both` is the default: either end is enough, so a
site with an iron stave takes iron from anywhere and sends it anywhere, and only
two sites that both lack iron cannot pass it between them. `Receive` is the
sharper original rule — only the destination counts, so outposts are one-way.
`Deliver` mirrors that, for supplying a frontier rather than feeding a capital.

### Known gaps

Clearance and `MaterialFlow` have both now been played on a real network, which
were the two things holding this back. `LogNetworkSync` defaults **off** as of
that confirmation — turn it on before reporting anything about portals
disagreeing between machines.

What is left is judgement rather than correctness, and is listed in TESTING.md:
the §4 costs have never been tuned, and whether `Both` is the right shipped
default is an open question about how it feels over a long game.
