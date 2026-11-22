# Asset Store listing — DRAFT (not submitted)

**Status: draft only. No Asset Store listing exists yet, no sales and no
reviews have been claimed. Do not publish this text until every item in
`SELLING_REQUIREMENTS.md` has passed.**

---

## Title

Sdx17 Matchmaking

## Short description (under 200 chars)

Local matchmaking for Unity: queue, lobbies, parties, skill-based matching, ready-check and timeouts — with a swappable backend interface. (138 chars)

## Long description

**Sdx17 Matchmaking** adds queue, lobby, party and skill-based matchmaking
to your Unity game — without locking you into a backend service.

Drop a `MatchmakingService` into your scene, create a
`MatchmakingConfig`, and your game can queue players, form lobbies,
keep parties together, match by skill rating, and run a ready-check
before the match starts. Game code talks to one clean facade; the
backend behind it is an interface you can swap.

**What you get**

- **Queue, lobbies and parties** — players queue per mode and region;
  players sharing a `PartyId` are matched together or never split.
- **Skill-based matching** — an Elo-style rating with a tolerance that
  widens the longer the oldest player has waited, so queues resolve
  instead of timing everyone out.
- **Ready-check** — every player must accept within a configurable
  timeout. A decline dissolves the lobby and automatically re-queues
  the players who had accepted; only the decliner is left out.
- **Queue timeouts** — players waiting too long are removed and your
  UI is told why.
- **A backend interface, not a vendor lock-in** — `IMatchmakingBackend`
  is the contract. The included `MockMatchmakingBackend` is an
  in-memory queue for local testing; swap in your own server backend
  with one call (`SetBackend(...)`) and your game code does not change.
- **Configurable in the Inspector** — match size, modes, regions,
  timeouts and skill tolerance on a `MatchmakingConfig`
  ScriptableObject, with a validating custom inspector.
- **Editor helpers** — `Sdx17 > Matchmaking` menu items to create the
  config and add the service to a scene.
- **Demo and self-test included** — a demo script with on-screen
  buttons and bot players, plus a Play-mode self-test that logs
  PASS/FAIL per check (34 checks, per the README).

**What it is NOT — please read before you buy**

- This is **not a production matchmaker out of the box**. The included
  backend is local-only: no server, no persistence, no anti-cheat, no
  cross-device play. Every player must be in the same Unity process.
  A real server backend implementing `IMatchmakingBackend` is required
  before launch — the guide explains how.
- This is **not a networking layer**. Matchmaking decides *who* plays
  together; your multiplayer transport decides *how* they connect.
- Version 0.1.0 is **untested in Unity** — it was built without a Unity
  editor available, so it has not been compiled or run inside Unity yet.
  The README and guide include a test checklist to run before you rely
  on it. Please run it and report any issue; the package is priced for
  a first release, not for a battle-tested track record.

**Good for:** local multiplayer prototypes, game jams, learning
matchmaking flow, and any Unity game that wants a clean queue → lobby →
ready-check → match-found flow with a backend it can replace later.

## Keywords (10)

1. matchmaking
2. multiplayer
3. lobby
4. queue
5. party
6. skill-based
7. ready check
8. matchmaker
9. PvP
10. session

## Category

Tools / Game Toolkits (suggested — re-check at listing time; Unity may
place it under a different Tools sub-category, and the category list
can change).

## Suggested price

**$35**

### Price justification (honest)

- A new publisher with no reviews should price in the **$15–$35** band
  to get first reviews. **$35 is the top of that band** — it is
  justified only by the scope in this package (ready-check + party
  handling + skill rating with widening tolerance + a swappable
  backend interface + demo and self-test), not by a track record,
  because there is none yet.
- At $35, the publisher's net is **70% = $24.50 per sale before tax
  and refunds** (Unity's standard 70/30 revenue share).
- This price **does not guarantee sales**. A new publisher with no
  reviews, an untested v0.1.0, and a local-only mock backend should
  expect slow first sales. The price should be re-checked against
  comparable matchmaking / lobby / multiplayer-toolkit assets
  immediately before listing — if comparable, tested assets are
  selling for less, price below them; do not hold $35 on principle.
- Consider a launch discount or a lower introductory price if the
  first weeks bring no reviews. Do not raise the price until the
  package is tested in Unity and has at least one verified review.

## What's included

- `Runtime/` — `PlayerInfo`, `Lobby`, `MatchmakingConfig`,
  `IMatchmakingBackend` (+ `ITickableBackend`), `MockMatchmakingBackend`,
  `MatchmakingService` (namespace `Sdx17.Matchmaking`).
- `Editor/` — validating config inspector + `Sdx17 > Matchmaking`
  menu items.
- `Samples~/Demo/` — `DemoMatchmaking` demo script with bot players
  and a scene-setup README (no `.unity` scene file is included; the
  scene is built from the steps in the demo README).
- `Tests~/Runtime/` — `MatchmakingSelfTest` Play-mode self-test
  (34 checks, per the README).
- `Documentation~/GUIDE.md` — full guide: quickstart, concepts, API
  reference, config reference, integration notes, FAQ, performance
  notes, limitations.
- `README.md`, `CHANGELOG.md`, `LICENSE` (MIT),
  `ThirdPartyNotices.txt`, `package.json`
  (`com.sdx17.matchmaking` 0.1.0).

## Requirements / Dependencies

- **Unity 2022.3 LTS** (the package targets `unity: 2022.3` in
  `package.json`).
- **Render pipeline:** URP / Built-in compatible. The package contains
  no shaders, materials or render-pipeline-specific code.
- **No paid dependencies. No bundled dependencies.** No third-party
  code, fonts, audio or assets are included (see
  `ThirdPartyNotices.txt`).
- **Mock backend included; real backend NOT included.** A server
  backend implementing `IMatchmakingBackend` is required for a
  production launch with players on different devices.
- **No other Sdx17 system is required.** The guide describes how this
  package integrates with the other Sdx17 systems (Multiplayer
  Architecture, Combat, Admin, UI, Inventory, Quest, NPC, Dialogue,
  Secure Save, Terrain Tools, Developer Plugin), but none of them is a
  dependency — matchmaking works standalone.

## Demo scene description

The included demo is a minimal, code-driven scene you build yourself
in under 5 minutes (steps in `Samples~/Demo/README.md`): one
`MatchmakingService` GameObject, one `MatchmakingConfig` asset, and
one `DemoMatchmaking` GameObject with on-screen buttons. Press Play →
**Join Queue** → **Add Bots (so a match can form)** → a ready-check
starts → **Accept Ready-Check**. The status box and the Console show
`MATCH FOUND!` when the flow works. Note honestly: no `.unity` scene
file is shipped, and demo bots never accept a ready-check on their own,
so the demo alone cannot complete a match — the included Play-mode
self-test covers the full accept flow. Real screenshots and a real
Unity recording of this demo are required before listing (see
`SELLING_REQUIREMENTS.md`); no AI images may be presented as
screenshots.

## Support contact

**Sdx17 — GitHub: sdx-rf17** (https://github.com/sdx-rf17)

No fake email address is listed here — do not invent one. Support is
via the publisher's GitHub profile / the repository for this package.
When filing an issue, include: the exact error text, your Unity
version, the steps to reproduce, and whether you ran the README test
checklist.

## Version history

### 0.1.0 (2026-11-21) — first release

- `IMatchmakingBackend` interface plus `MockMatchmakingBackend`
  (in-memory queue, local testing only).
- `Lobby` model with states Waiting, ReadyCheck, InMatch, Closed.
- `PlayerInfo` with skill rating, region, and party id.
- `MatchmakingConfig` ScriptableObject: match size, queue timeout,
  ready-check timeout, skill tolerance and growth, allowed modes
  and regions.
- `MatchmakingService` MonoBehaviour facade: join/leave/cancel queue,
  ready-check accept/decline, lobby create/join/leave, per-frame
  ticking of the backend.
- Skill-based matching: parties kept together, lobbies form from
  players whose ratings fit inside a tolerance that widens the
  longer the oldest player has waited.
- Ready-check flow: all players must accept; a decline or timeout
  dissolves the lobby and re-queues the players who accepted.
- Queue timeout: a player waiting longer than the timeout is removed
  and `OnQueueTimeout` fires.
- Editor support: custom inspector for `MatchmakingConfig` with
  validation, and `Sdx17 > Matchmaking` menu items.
- Demo sample and Play-mode self-test (34 checks, per the README).
- **Known status: UNTESTED IN UNITY.** No Unity editor was available
  in the build environment; nothing has been compiled or run inside
  Unity yet.

---

*Draft only — not submitted to the Asset Store. Re-check the price,
category, keywords and requirements at listing time.*
