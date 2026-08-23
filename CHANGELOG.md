# Changelog

## 0.1.0 — unreleased

First build. Both features work and have been played in single player; the
clearance half has not yet been exercised across a real network.

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

### Known gaps

- Clearance has never been tested with two machines connected.
- `LogNetworkSync` defaults **on** and narrates the portal sync into the log. It
  will default off once the above has been verified.
