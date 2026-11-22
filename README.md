# Sdx17 Matchmaking

**Version 0.1.0 — UNTESTED IN UNITY**

Local matchmaking for Sdx17 Unity games: queue, lobbies, party support,
skill-based matching, ready-check, and timeouts. Built for Unity 2022.3 LTS,
C#, URP-compatible, with no paid dependencies.

> No Unity editor was available in the build environment, so this package has
> **not been compiled or run inside Unity yet**. Treat it as a v0.1.0 draft:
> follow the test checklist below in Unity before using it in a real project.

## What it is

- A `MatchmakingService` MonoBehaviour your game talks to: join/leave/cancel
  the queue, accept/decline ready-checks, create/join/leave lobbies.
- An `IMatchmakingBackend` interface, so the local backend can be swapped for
  a real server backend without touching game code.
- A `MockMatchmakingBackend`: an in-memory queue that forms lobbies from
  players in the same mode + region, keeps parties together, matches by skill
  rating with a tolerance that widens over wait time, and runs ready-checks
  and queue timeouts.
- A `MatchmakingConfig` ScriptableObject for all tunables (match size, modes,
  regions, timeouts, skill tolerance).
- Editor support: a validating inspector for the config and
  `Sdx17 > Matchmaking` menu items.
- A demo sample and a Play-mode self-test that logs PASS/FAIL per check.

## What it is NOT

- **Not a production matchmaker.** The mock backend is local-only: no server,
  no persistence, no anti-cheat, no cross-device play. Every player must be
  in the same Unity process. A real backend implementing
  `IMatchmakingBackend` is required before launch.
- **Not tested in Unity yet.** See the checklist below.
- **Not a networking layer.** Matchmaking decides *who* plays together; your
  multiplayer transport decides *how* they connect.

## Setup (under 5 minutes)

1. Copy this package into your Unity project's `Packages/` folder
   (or add it via Package Manager > Add package from disk, pointing at
   `package.json`).
2. In Unity: `Sdx17 > Matchmaking > Add Matchmaking Service to Scene`.
3. `Sdx17 > Matchmaking > Create Matchmaking Config`, then drag the config
   asset into the service's `Config` field.
4. In your code, set the local player and subscribe to events:

```csharp
var service = FindObjectOfType<MatchmakingService>();
service.LocalPlayer = new PlayerInfo("player-123", "Sadek", 1000f, "eu-west");
service.MatchFound += lobby => StartGame(lobby);
service.QueueTimedOut += player => ShowMessage("Queue timed out, try again.");
service.ReadyCheckStarted += lobby => ShowReadyCheckUI(lobby);

service.JoinQueue();            // uses config DefaultMode / DefaultRegion
service.AcceptReadyCheck();     // when the ready-check UI is accepted
service.LeaveQueue();           // or service.CancelQueue();
```

## Files

| Path | What it is |
| --- | --- |
| `package.json` | UPM package manifest (`com.sdx17.matchmaking` 0.1.0) |
| `Runtime/PlayerInfo.cs` | Player data: id, display name, skill rating, region, party id |
| `Runtime/Lobby.cs` | Lobby model: players, mode, region, max players, state machine |
| `Runtime/MatchmakingConfig.cs` | ScriptableObject with all tunables + validation |
| `Runtime/IMatchmakingBackend.cs` | Backend contract + events + `ITickableBackend` |
| `Runtime/MockMatchmakingBackend.cs` | In-memory backend (local testing only) |
| `Runtime/MatchmakingService.cs` | MonoBehaviour facade the game talks to |
| `Runtime/Sdx17.Matchmaking.Runtime.asmdef` | Runtime assembly definition |
| `Editor/MatchmakingConfigEditor.cs` | Validating custom inspector for the config |
| `Editor/MatchmakingMenu.cs` | `Sdx17 > Matchmaking` menu items |
| `Editor/Sdx17.Matchmaking.Editor.asmdef` | Editor assembly definition |
| `Samples~/Demo/DemoMatchmaking.cs` | Demo script with on-screen buttons + bot players |
| `Samples~/Demo/README.md` | Demo scene setup steps |
| `Tests~/Runtime/MatchmakingSelfTest.cs` | Play-mode self-test, logs PASS/FAIL |
| `Tests~/Runtime/Sdx17.Matchmaking.Tests.asmdef` | Test assembly definition |
| `CHANGELOG.md`, `LICENSE`, `.gitignore` | Release notes, MIT licence, Unity ignore rules |

## Config options

All set on the `MatchmakingConfig` asset (defaults in brackets):

| Field | Meaning |
| --- | --- |
| `MaxPlayersPerMatch` [4] | Players per lobby. Parties are never split to fit. |
| `MinPlayersPerMatch` [2] | Minimum players before a match can form. |
| `AllowedModes` [duel, team, ffa] | Modes the backend accepts. |
| `AllowedRegions` [eu-west, eu-east, na-east] | Regions the backend accepts. |
| `DefaultMode` [duel] / `DefaultRegion` [eu-west] | Used when `JoinQueue()` is called with no arguments. |
| `QueueTimeoutSeconds` [60] | A player waiting longer is removed; `OnQueueTimeout` fires. |
| `ReadyCheckTimeoutSeconds` [15] | Time for all players to accept a proposed match. |
| `SkillTolerance` [100] | Max rating difference for a match at queue time. |
| `SkillToleranceGrowthPerSecond` [5] | Extra tolerance per second the oldest waiter has waited. |
| `MaxSkillTolerance` [400] | Cap on the widened tolerance. |
| `EnableReadyCheck` [true] | Off = matches start immediately when formed. |

## Public API

```csharp
// Service (MonoBehaviour) — the only class game code needs
service.LocalPlayer = new PlayerInfo(playerId, displayName, skillRating, region, partyId);
service.JoinQueue(mode, region);          // bool — false = already queued / invalid mode or region
service.JoinQueue(player, mode, region);  // queue any player (bots, tests)
service.LeaveQueue();                     // bool
service.CancelQueue();                    // bool — same effect as LeaveQueue
service.AcceptReadyCheck();               // bool
service.DeclineReadyCheck();              // bool
service.CreateLobby(mode, region, maxPlayers); // Lobby, or null on invalid input
service.JoinLobby(lobbyId);               // bool
service.LeaveLobby();                     // bool
service.SetBackend(IMatchmakingBackend);  // swap in a real server backend

// Events (C# events; UnityEvents with the same names exist in the Inspector)
service.MatchFound          // Action<Lobby> — match confirmed, start the game
service.QueueTimedOut       // Action<PlayerInfo>
service.ReadyCheckStarted   // Action<Lobby> — show the accept/decline UI
service.ReadyCheckCompleted // Action<Lobby>
service.ReadyCheckFailed    // Action<Lobby, PlayerInfo> — who declined / timed out

// Backend interface — implement this for a real server backend
IMatchmakingBackend.JoinQueue / LeaveQueue / CancelQueue
IMatchmakingBackend.AcceptReadyCheck / DeclineReadyCheck
IMatchmakingBackend.CreateLobby / JoinLobby / LeaveLobby / GetLobby
```

## How matching works

1. Players queue per mode + region; queueing across modes or regions never
   mixes.
2. On each tick the backend groups queued players into parties (same
   `PartyId` = one unit; solo players are one-person parties), sorts parties
   by average rating, and greedily fills a lobby up to `MaxPlayersPerMatch`.
3. The lobby forms only if the rating spread of everyone picked fits inside
   the tolerance for the oldest waiter's wait time
   (`SkillTolerance + growth × waited`, capped at `MaxSkillTolerance`).
4. With the ready-check on, the lobby enters `ReadyCheck` and every player
   must accept within `ReadyCheckTimeoutSeconds`. If all accept, the lobby
   becomes `InMatch` and `OnMatchFound` fires. If anyone declines or time
   runs out, the lobby is dissolved and the players who had accepted are
   re-queued automatically; the decliner is not.
5. A player waiting longer than `QueueTimeoutSeconds` is removed and
   `OnQueueTimeout` fires.

## Integration notes

- **Multiplayer Architecture:** when `OnMatchFound` fires, hand the lobby's
  player list to your session/transport layer to start the game. Matchmaking
  decides who plays together; the multiplayer layer decides how they connect.
  Keep `MatchmakingService` on a GameObject that lives for the whole session,
  next to your network manager.
- **Combat:** nothing here touches combat. Once the match starts, combat
  systems read the lobby roster (or your own player list) as usual.
- **Skill rating:** `PlayerInfo.SkillRating` is a plain number (Elo-style,
  starting at 1000). The package does not update ratings after a match —
  your game should call `PlayerInfo.ApplyRatingDelta(...)` with its own
  win/loss formula and persist the result.
- **Parties:** set the same `PartyId` on `PlayerInfo` for players queueing
  together. A party larger than `MaxPlayersPerMatch` can never match.

## Test checklist (run in Unity before calling this done)

- [ ] Package imports with no compile errors (check the Console).
- [ ] `Sdx17 > Matchmaking > Create Matchmaking Config` creates the asset;
      the custom inspector shows validation messages.
- [ ] Demo scene (see `Samples~/Demo/README.md`): Join Queue → Add Bots →
      ready-check starts → Accept → `MATCH FOUND!` logs in the Console.
- [ ] Attach `MatchmakingSelfTest` to a GameObject in a test scene, press
      Play, and confirm the Console shows `DONE: 34 passed, 0 failed`
      (34 checks cover queueing, duplicates, invalid mode/region, match
      formation, skill-spread rejection, parties, ready-check accept/decline,
      timeout, and lobby create/join/leave).
- [ ] Decline a ready-check in the demo: the lobby dissolves and the
      accepters are re-queued.
- [ ] Leave the queue running with one player for 60 seconds: the queue
      timeout fires.

## Licence

MIT — Copyright 2026 Sdx17. See `LICENSE`.
