# Stavebound

**Travel to any portal in the world. What you may carry through is decided by the staves standing
at the far end — and every stave is bought with a boss's head.**

Interact with a portal and pick your destination off the map. The choice belongs to the portal and
applies to everyone, until someone re-aims it. Walking in travels.

Then the part that makes it more than another any-portal mod. Build an **Elder's Stave** beside a
portal and that portal will accept copper, tin and bronze. It will still refuse iron — until you go
and build a **Bonemass's Stave** there too, and it says so in as many words:

> Iron cannot enter "Copper Mine" — no Bonemass's Stave there.

**Only the destination is ever checked.** An outpost with no staves can send ore to your base
forever and never receive any. That asymmetry is the whole point: ore flows *inward*, toward the
places you have invested in, and outposts stay cheap and disposable.

You are told before you commit, not at the wall. The portal's runes go dark when it will refuse what
you are holding, the offending stacks are marked in your inventory while you pack, and walking up to
the portal gets you the reason in words.

> **One caveat.** Clearance has been played thoroughly in single player but has not yet been tested
> with two machines connected. It is safe to try on a server; just do not be shocked by a rough edge.

## Installing

Needs **BepInEx** and **Jotunn**. If you are using a mod manager both come as dependencies and there
is nothing else to do. By hand, drop `Stavebound.dll` into `BepInEx/plugins`.

**Install it on the server and on every client.** The server works out clearance; clients need the
map selector and the travel check. Clearance rules synchronise from the server, so nobody can loosen
them locally.

Removing the mod removes its pieces, so any staves you built will vanish — normal for any mod that
adds buildables. The extra data it writes is harmless to an unmodded game.

## The staves

Each is built from that biome boss's trophy plus a little of what the biome gives you. Stand one
within ten metres of a portal and it binds to it.

| Stave | Costs | Lets through |
|---|---|---|
| **Elder's** | The Elder trophy · 10 copper · 20 stone | Copper, tin, bronze |
| **Bonemass's** | Bonemass trophy · 10 iron · 20 stone | Iron and scrap iron |
| **Moder's** | Moder trophy · 10 silver · 20 stone | Silver, dragon eggs |
| **Yagluth's** | Yagluth trophy · 10 black metal · 20 stone | Black metal |
| **Queen's** | The Queen trophy · 3 dvergr extractors · 20 stone | Dvergr extractors, mechanical springs |
| **Ashen** | Fader trophy · 10 flametal · 20 stone | Flametal and the Ashlands' spoils |

Tiers are independent — a site can accept silver while still refusing iron. Nothing makes you climb
the ladder in order.

While you are holding one, a beam shows which portal it would bind to, and a circle shows its reach
if nothing is close enough.

## Controls

| | |
|---|---|
| **E** at a portal | Open the destination selector |
| **Shift+E** at a portal | Rename it, as vanilla |
| **← →** | Change the highlighted destination |
| **P** | Confirm |
| **Escape** | Cancel |
| **O** | Sort by distance or name |
| **K** | Show only destinations that accept what you are carrying |

All rebindable under `5 - Selector keys`, each with a gamepad button beside it. The selector is fully
playable on a pad.

## Settings worth knowing

| Setting | Does |
|---|---|
| `ReaimPermission` | Who may re-aim a portal — anyone, only players a guard stone permits, or admins |
| `StaveRadius` | How far a stave reaches for its portal. Ten metres by default |
| `PortalBinding` | Whether a rune binds to the nearest portal or every portal in range |
| `StrictLadder` | Off by default. On, a site's clearance stops at its first missing rung |
| `SeamlessTransit` | Off by default. Ends a trip when the destination has loaded rather than on vanilla's eight-second timer — a destination already in memory skips the loading screen entirely |
| `ShowBlockedCargoOverlay` | Marks the stacks a nearby portal's destination will refuse |
| `HidePortalNames` | Hides names in the selector, if you would rather navigate by the map |

Which item belongs to which stave is configurable too, under `2 - Clearance`. The list of blocked
items is never hand-written — it is read from the game at startup, so a game update adding a new ore
cannot break the mod. Anything unrecognised is held to the highest tier and named in the log.

While the mod is pre-release, `LogNetworkSync` defaults **on** and narrates portal syncing into the
log. Turn it off if you would rather it were quiet.

## Console commands

Type `help` in the F5 console for the full list. The useful ones:

- `stave_portals` — every portal this game knows about, where it points, and its clearance
- `stave_items` — every item the game refuses to teleport, and which stave permits it
- `stave_net` — the sync's state. Run it on a server and a client and compare

## A note on cheating

Cargo checks happen on your own machine, because that is where your inventory is. A determined player
could bypass them. This is a rule system for playing with people you like, **not anti-cheat** — the
server owns what each site permits, and never what you are carrying.

## Licensing

Code: **MIT** — see [LICENSE](LICENSE).

Valheim and its assets are the property of Iron Gate Studio. No game files are redistributed here.
The mod builds its pieces by recolouring one that already exists in the game rather than shipping any
art of its own.
