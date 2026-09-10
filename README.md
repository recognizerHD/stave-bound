# Stavebound

**Travel to any portal in the world. What you may carry through is decided by the staves standing
at the far end — and every stave is bought with a boss's head.**

Interact with a portal and pick your destination off the map. The choice belongs to the portal and
applies to everyone, until someone re-aims it. Walking in travels.

Then the part that makes it more than another any-portal mod. Build an **Elder's Stave** beside a
portal and that portal will accept copper, tin and bronze. It will still refuse iron — until you go
and build a **Bonemass's Stave** there too, and it says so in as many words:

> Iron cannot enter "Copper Mine" — no Bonemass's Stave there.

**One end of the trip has to have paid.** By default either end will do, so a site with an iron stave
both takes iron from anywhere and sends it anywhere — and only two sites that *both* lack it cannot
pass iron between them. Which end is asked is the dial most worth knowing about; see
[Which end pays](#which-end-pays).

You are told before you commit, not at the wall. The portal's runes go dark when it will refuse what
you are holding, the offending stacks are marked in your inventory while you pack, and walking up to
the portal gets you the reason in words.

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

## Which end pays

`MaterialFlow`, under `2 - Clearance`. One rule for the whole world, set on the server — this is the
setting that decides how the whole mod feels, so it is worth a minute before you start a save.

| | Asks | What that gives you |
|---|---|---|
| **`Both`** *(default)* | Either end | A site's staves cover arriving **and** departing, so metals move freely between it and anywhere else. Only two sites that both lack a tier cannot pass it. The most forgiving — a stave you forgot to build strands nothing |
| **`Receive`** | The destination | The sharper rule, and the one the mod was designed around. An outpost with no staves sends ore to your base forever and never receives any. Ore flows **inward**, toward the places you have invested in, and outposts stay cheap, disposable and one-way |
| **`Deliver`** | The portal you leave | `Receive` mirrored. A stocked base supplies a bare frontier with anything, but that frontier cannot ship its own ore home until it has staves of its own |

Under every one of them, a tier that **neither** end holds never moves. Nothing here lets you carry
something nobody paid for; it only decides who is allowed to have done the paying.

One thing `Both` gives up, since it is the default and this is easy to discover the hard way: a
single fully-staved base makes your whole network permeable in two hops — outpost to base, base to
other outpost. Direct outpost-to-outpost still needs one of them to have paid. If that reads as too
loose once you have played it, `Receive` is a one-line change and restores the original rule exactly.

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
| `MaterialFlow` | Which end of a trip a site's staves count for. `Both` by default — either end is enough. `Receive`: only the destination, so outposts are one-way. `Deliver`: only the portal you leave |
| `StaveRadius` | How far a stave reaches for its portal. Ten metres by default |
| `PortalBinding` | Whether a stave binds to the nearest portal or every portal in range |
| `StrictLadder` | Off by default. On, a site's clearance stops at its first missing rung |
| `SealedItems` | Resources no stave will ever carry. Bloodgold and Petrified Tissue by default |
| `SeamlessTransit` | Off by default. Ends a trip when the destination has loaded rather than on vanilla's eight-second timer — a destination already in memory skips the loading screen entirely |
| `ShowBlockedCargoOverlay` | Marks the stacks a nearby portal's destination will refuse |
| `HidePortalNames` | Hides names in the selector, if you would rather navigate by the map |

Two resources are **sealed**: the Deep North's Bloodgold and Petrified Tissue. No stave carries them
and none ever will — the base game moves those by stone portal and nothing else, and this mod leaves
that alone rather than selling them back to you for a trophy. `SealedItems` decides which, if you
disagree.

Which item belongs to which stave is configurable too, under `2 - Clearance`. The list of blocked
items is never hand-written — it is read from the game at startup, so a game update adding a new ore
cannot break the mod. Anything unrecognised is held to the highest tier and named in the log.

`LogNetworkSync` defaults **off**. Turn it on before reporting anything about two machines
disagreeing about a portal — it narrates every sweep, broadcast and receive, and it is the first
thing anyone will ask for.

Note that changing a default only affects a config file that does not exist yet. If you have played
an earlier build, your existing `com.recognizerhd.stavebound.cfg` keeps whatever it was written with
— edit it, or delete it and let the game write a fresh one.

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
