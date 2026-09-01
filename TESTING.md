# What has not been tested

Everything here is **built and believed to work, and unverified in the conditions that matter**. This
file is the list to work through before 0.1.0 goes anywhere; it is not packaged in the Thunderstore
zip, and it should shrink to nothing rather than being tidied away.

Two things gate most of it:

- **The dedicated server would not start.** The client is fine. Nothing suggests this is a mod
  problem, but until a local server runs, no networked claim below can be checked at all.
- **`MaterialFlow` has never been played.** It was reasoned about, built and compiled. Not one trip
  has been taken under it, in single player or otherwise.

Contributors: do not mark anything here done from a code reading. Every line is here because reading
the code already convinced someone, and that turned out not to be enough.

---

## 1. `MaterialFlow` — the new setting, single player first

Runs entirely on one machine, so **none of it is blocked on the server**. Do this first; it is the
cheapest way to find out whether the rule is right before compounding it with network questions.

Set up two portals: **A** with a Bonemass's Stave in range, **B** with no staves at all. Carry iron.
The nine trips below are the whole truth table.

| # | `MaterialFlow` | Trip | Expected |
|---|---|---|---|
| 1.1 | `Receive` | B → A | **Allowed** — the destination has iron |
| 1.2 | `Receive` | A → B | **Refused**, naming Bonemass's Stave |
| 1.3 | `Receive` | A → A′ (a second iron site) | **Allowed** |
| 1.4 | `Deliver` | A → B | **Allowed** — the portal you leave has iron |
| 1.5 | `Deliver` | B → A | **Refused** |
| 1.6 | `Deliver` | A → A′ | **Allowed** |
| 1.7 | `Both` | A → B | **Allowed** |
| 1.8 | `Both` | B → A | **Allowed** |
| 1.9 | `Both` | B → B′ (neither has iron) | **Refused** |

Rows 1.2, 1.5 and 1.9 are the ones that matter — they are the only three refusals, and a bug that
makes the gate permissive shows up nowhere else.

Then, for each mode, confirm all five surfaces agree with the table. **They are driven from one
function (`ClearanceGate.EffectiveMask`) precisely so they cannot disagree, which means a
disagreement is a real bug and not a cosmetic one:**

- [ ] The portal's runes go dark exactly when the trip would be refused
- [ ] Walking up gives the named refusal, and names the right stave
- [ ] Inventory stacks are marked when, and only when, that trip would refuse them
- [ ] The selector's per-row verdict matches what actually happens on walking in
- [ ] The cargo filter (`K`) hides exactly the destinations that would refuse

And the panel line:

- [ ] Under `Both` and `Deliver` the selector shows the flow note; under `Receive` it does not
- [ ] A destination with no chips still reads green under `Both` when the departure portal has the tier
      — this is the case the note exists to explain, and the one most likely to read as a bug

### Interactions to check while there

- [ ] `StrictLadder = true` with `MaterialFlow = Both`. The union of two masks should never open a
      tier that a gap ought to have closed. The reasoning says it cannot — each mask is already
      trimmed to a prefix on the server, and the union of two prefixes is the longer prefix — so this
      is confirming an argument, not exploring
- [ ] `m_allowAllItems` portals still let everything through under every mode
- [ ] Changing `MaterialFlow` mid-session takes effect on the next trip with no relog

---

## 2. Clearance across a real network

**Blocked on the server.** The registry sync was proven on two machines *before* masks existed, so no
client has ever received a non-zero mask. This is the single largest untested claim in the mod.

Copy the current DLL to the client first. A stale client will not announce itself: version numbers
have not moved, and `Clearance.Ashen` shifted from `1<<4` to `1<<5` during the Queen's Stave work, so
an old build silently disagrees about which tier is which.

- [ ] A client sees a **non-zero** clearance mask on a portal it has never visited
- [ ] A client is refused by a mask its own machine never computed
- [ ] A client is permitted by one, and the trip completes
- [ ] A stave built by the host reaches the client's view within ~10 s (the site sweep interval)
- [ ] A stave *destroyed* on the host clears on the client — the sweep writes only on change, so this
      is a different path from the one above
- [ ] `stave_net` on server and client agree
- [ ] `MaterialFlow` is genuinely admin-only: changing it client-side does nothing, and the server's
      value wins. It is `Synced`, so this is confirming Jotunn's behaviour, not ours

Once **all** of the above pass, in the same change: turn `LogNetworkSync` off by default, and drop the
caveat from README.md and the "Known gaps" section of CHANGELOG.md.

---

## 3. Balance — wants sessions, not checklists

Open questions that only real play answers. Nothing here is a bug.

- [ ] Is `Both` the right shipped default? It retires the one-way outpost as the law. See DESIGN.md §10
- [ ] The two-hop hub: under `Both`, one fully-staved capital makes a whole network permeable via
      A → capital → B. Does that feel earned, or does it hollow out the per-site journey?
- [ ] The §4 costs — a trophy and ten of the metal — are placeholders and have never been tuned
- [ ] Does `Deliver` have a real audience, or is it a symmetry nobody plays?

---

## 4. Standing gaps, older than this change

- [ ] Gamepad navigation of the selector has been exercised far less than keyboard
- [ ] The `ObjectDB` tier map has only been read on game 0.221.12; a game update adding a blocked item
      should surface it as an "unclassified" warning naming the prefab, and that path has never fired
      for real
- [ ] Conflict detection warns by GUID, and has never been run against an actually-installed
      conflicting mod
- [ ] Seamless transit has been played, but not with a client whose destination is unloaded on a
      server — the case it exists for
