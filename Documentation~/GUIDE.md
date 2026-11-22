# Sdx17 Matchmaking — Guide

**Package:** `com.sdx17.matchmaking` · **Version:** 0.1.0 · **Unity:** 2022.3 LTS
**Status: v0.1.0 — UNTESTED IN UNITY.** No Unity editor was available in the
build environment, so this package has not been compiled or run inside Unity
yet. Follow the test checklist in the README before using it in a real
project, and do not ship a game on it until you have run that checklist
yourself. Nothing in this guide claims otherwise.

> Scope in one sentence: matchmaking decides **who** plays together — queue,
> lobbies, parties, skill-based matching, ready-check, timeouts. It does not
> decide **how** they connect (that is your multiplayer transport), and the
> included backend is local-only (see "The mock backend is local-only").

---

## Table of contents

1. [Quickstart (5 minutes)](#1-quickstart-5-minutes)
2. [Concepts](#2-concepts)
3. [API reference](#3-api-reference)
4. [Config reference](#4-config-reference)
5. [The matching algorithm, step by step](#5-the-matching-algorithm-step-by-step)
6. [Integration with the other Sdx17 systems](#6-integration-with-the-other-sdx17-systems)
7. [Swapping in a real backend](#7-swapping-in-a-real-backend)
8. [Demo scene & self-test](#8-demo-scene--self-test)
9. [Troubleshooting / FAQ](#9-troubleshooting--faq)
10. [Performance notes](#10-performance-notes)
11. [Limitations (read before you buy / ship)](#11-limitations-read-before-you-buy--ship)

---

## 1. Quickstart (5 minutes)

### Step 1 — Install the package

Copy this package into your Unity project's `Packages/` folder, or use
Package Manager → **Add package from disk** and point at `package.json`.

### Step 2 — Add the service to your scene

`Sdx17 > Matchmaking > Add Matchmaking Service to Scene`

This creates a GameObject with a `MatchmakingService` component. Put it on a
GameObject that lives for the whole session — for example next to your
network / multiplayer manager — and do not destroy it between scenes
unless you intentionally want to reset matchmaking state.

### Step 3 — Create a config

`Sdx17 > Matchmaking > Create Matchmaking Config`

Drag the created `MatchmakingConfig` asset into the service's **Config**
field in the Inspector. If you skip this, the service logs a warning and
uses built-in defaults (see §4) — it still works, but you should always
assign a real config so your tunables are visible and versioned.

### Step 4 — Wire up the local player and the events

```csharp
using Sdx17.Matchmaking;
using UnityEngine;

public class MyMatchmakingUI : MonoBehaviour
{
    [SerializeField] private MatchmakingService service;

    private void Start()
    {
        // Identity is the stable PlayerId (account id), not the display name.
        service.LocalPlayer = new PlayerInfo("player-123", "Sadek", 1000f, "eu-west");

        service.MatchFound          += lobby => StartGame(lobby);
        service.QueueTimedOut       += player => ShowMessage("Queue timed out, try again.");
        service.ReadyCheckStarted   += lobby => ShowReadyCheckUI(lobby);
        service.ReadyCheckCompleted += lobby => HideReadyCheckUI();
        service.ReadyCheckFailed    += (lobby, cause) =>
            ShowMessage($"{cause?.DisplayName ?? "A player"} declined — re-queueing.");
    }

    public void OnPlayButton() => service.JoinQueue(); // uses DefaultMode / DefaultRegion
    public void OnAcceptButton() => service.AcceptReadyCheck();
    public void OnDeclineButton() => service.DeclineReadyCheck();
    public void OnCancelButton() => service.LeaveQueue(); // or service.CancelQueue();

    private void StartGame(Lobby lobby)
    {
        // Hand lobby.Players to your multiplayer transport / session layer.
        // Matchmaking does NOT start the network session for you.
    }
    private void ShowMessage(string m) { /* your UI */ }
    private void ShowReadyCheckUI(Lobby l) { /* your UI, with a countdown */ }
    private void HideReadyCheckUI() { /* your UI */ }
}
```

You can also wire the four UnityEvents of the same names
(`onMatchFound`, `onQueueTimeout`, `onReadyCheckStarted`,
`onReadyCheckCompleted`) in the Inspector instead of subscribing in code.
Note: there is **no** Inspector UnityEvent for ready-check failure — only
the C# event `ReadyCheckFailed` (see §3.6).

### Step 5 — Play

Press Play, call `JoinQueue()`, and add enough players (or bots in the
demo, §8) for a match to form. When the ready-check starts, every player
must accept within `ReadyCheckTimeoutSeconds` (default 15 s) or the lobby
dissolves.

---

## 2. Concepts

### PlayerInfo — one player as matchmaking sees them

- `PlayerId` is the stable identity (account id). Two players with the same
  `PlayerId` are the same player to the queue; a duplicate join is rejected.
- `DisplayName` is cosmetic. If you pass an empty display name, the
  constructor falls back to `PlayerId`.
- `SkillRating` is a plain Elo-style number (default 1000). Higher means
  stronger. The matchmaker only ever compares ratings of players in the
  same mode and region, never across them.
- `Region` defaults to `"eu-west"` when empty.
- `PartyId`: empty means solo. Players sharing a `PartyId` are matched
  together or not at all — a party is never split across lobbies. A party
  larger than `MaxPlayersPerMatch` can never match.
- `IsInParty` is true when `PartyId` is not empty.
- Ratings are **not** updated by matchmaking. After a match, call
  `PlayerInfo.ApplyRatingDelta(delta)` with your own win/loss formula and
  persist the result yourself (see §6, Secure Save).

### Lobby — one matched group

A lobby has an `Id` (a GUID string, generated in the constructor), a
`Mode`, a `Region`, a `MaxPlayers`, a `HostPlayerId` (the first player
added), a `Players` list, and a `State`. `CreatedAtUtc` records when it
was created.

`LobbyState` has four values, and `Closed` is terminal — a closed lobby
never reopens:

| State | Meaning |
| --- | --- |
| `Waiting` | Forming: players are being added, match not started. |
| `ReadyCheck` | Match proposed: every player must accept before play starts. |
| `InMatch` | All players accepted; the match is live. |
| `Closed` | Closed for good: finished, dissolved, or abandoned. |

`PlayerCount`, `IsFull` (`PlayerCount >= MaxPlayers`) and `IsEmpty`
(`PlayerCount == 0`) are read from the player list. Removing the last
player closes the lobby; removing the host promotes the next player in
the list to host.

### Queue — waiting to be matched

Players queue per **mode + region**. Queueing across modes or regions
never mixes. A player already in the queue, or currently in a
ready-check, cannot join again. A player waiting longer than
`QueueTimeoutSeconds` is removed and `OnQueueTimeout` fires.

### Ready-check — confirm before you play

When a match is proposed (and `EnableReadyCheck` is true), the lobby
enters `ReadyCheck` and every player must accept within
`ReadyCheckTimeoutSeconds`. If all accept, the lobby becomes `InMatch`
and `OnMatchFound` fires. If anyone declines or time runs out, the lobby
is dissolved and the players who had already accepted are **re-queued
automatically**; the decliner (or the player who timed out) is not.

If `EnableReadyCheck` is false, a formed match goes straight to
`InMatch` and `OnMatchFound` fires immediately — no ready-check events.

### Backend — the part you will replace for launch

Game code only ever talks to `IMatchmakingBackend` through
`MatchmakingService`. The included `MockMatchmakingBackend` is an
in-memory queue for local testing. A real server backend implements the
same interface, and you swap it in with `SetBackend(...)` — game code
never needs to change (see §7).

### Config — all tunables in one ScriptableObject

`MatchmakingConfig` holds match size, allowed modes/regions, timeouts
and skill tolerance. Full field list with defaults is in §4. The editor
inspector validates it (`MatchmakingConfigEditor`): values are clamped
on validate, and empty mode/region lists and invalid defaults are
repaired automatically.

---

## 3. API reference

All runtime types live in the `Sdx17.Matchmaking` namespace. This section
lists **every public class, method, property and event that exists in
`Runtime/`** — nothing here is invented; if a method is not listed, it
does not exist in v0.1.0.

> Return-value rule (applies everywhere): a `bool` return of `true` only
> means the request was **accepted**. The event tells you what actually
> happened. A `false` means the request was rejected (already queued,
> invalid mode/region, not in a lobby, no local player, and so on).
> `CreateLobby` returns `null` on invalid input instead of throwing
> (except for the `Lobby` constructor itself — see §3.2).

### 3.1 PlayerInfo (`Runtime/PlayerInfo.cs`)

```csharp
public class PlayerInfo
{
    public PlayerInfo(string playerId, string displayName, float skillRating,
                      string region, string partyId = null);
    public string PlayerId { get; }
    public string DisplayName { get; }
    public float  SkillRating { get; }
    public string Region { get; }
    public string PartyId { get; }
    public bool   IsInParty { get; }
    public void   ApplyRatingDelta(float delta);
}
```

- Constructor throws `ArgumentException` when `playerId` is empty.
  Empty `displayName` falls back to `playerId`; empty `region` falls back
  to `"eu-west"`; `partyId` is optional (`null` = solo).
- `ApplyRatingDelta(delta)`: adds `delta` to `SkillRating`, clamped at a
  minimum of 0. Positive delta = win. The package does not call this for
  you and does not persist the result.

### 3.2 Lobby (`Runtime/Lobby.cs`)

```csharp
public enum LobbyState { Waiting, ReadyCheck, InMatch, Closed }

public class Lobby
{
    public Lobby(string mode, string region, int maxPlayers, PlayerInfo host);
    public string Id { get; }
    public string Mode { get; }
    public string Region { get; }
    public int    MaxPlayers { get; }
    public LobbyState State { get; }
    public string HostPlayerId { get; }
    public IReadOnlyList<PlayerInfo> Players { get; }
    public int    PlayerCount { get; }
    public bool   IsFull { get; }
    public bool   IsEmpty { get; }
    public DateTime CreatedAtUtc { get; }
    public bool ContainsPlayer(string playerId);
    public bool TryAddPlayer(PlayerInfo player);
    public bool RemovePlayer(string playerId);
    // internal only: SetState(LobbyState), Close(), and a rebuilding constructor
    // used by backends. Game code should never call these.
}
```

- Constructor throws `ArgumentNullException` when `host` is null,
  `ArgumentOutOfRangeException` when `maxPlayers < 2`, and
  `ArgumentException` when `mode` is empty. Empty `region` falls back to
  `"eu-west"`. The host is added as the first player and becomes
  `HostPlayerId`.
- `TryAddPlayer(player)` returns `false` when `player` is null, the lobby
  is `Closed` or `InMatch`, the lobby is full, or the player is already
  in it. It does **not** check mode, region, skill or party — the backend
  owns those rules.
- `RemovePlayer(playerId)` returns `false` when the id is empty or the
  player is not in the lobby. Removing the host promotes the next player;
  removing the last player sets the state to `Closed`.
- `SetState` / `Close` and the second (rebuilding) constructor are
  `internal` — backends use them; game code must treat `Lobby.State` as
  read-only and react to events instead.

### 3.3 MatchmakingConfig (`Runtime/MatchmakingConfig.cs`)

```csharp
[CreateAssetMenu(fileName = "MatchmakingConfig",
                 menuName = "Sdx17/Matchmaking/Matchmaking Config")]
public class MatchmakingConfig : ScriptableObject
{
    public int      MaxPlayersPerMatch { get; }
    public int      MinPlayersPerMatch { get; }
    public string[] AllowedModes { get; }
    public string[] AllowedRegions { get; }
    public string   DefaultMode { get; }
    public string   DefaultRegion { get; }
    public float    QueueTimeoutSeconds { get; }
    public float    ReadyCheckTimeoutSeconds { get; }
    public float    SkillTolerance { get; }
    public float    SkillToleranceGrowthPerSecond { get; }
    public float    MaxSkillTolerance { get; }
    public bool     EnableReadyCheck { get; }
    public float ToleranceForWait(float waitedSeconds);
    public bool  IsModeAllowed(string mode);
    public bool  IsRegionAllowed(string region);
}
```

- `ToleranceForWait(waitedSeconds)` returns
  `min(SkillTolerance + SkillToleranceGrowthPerSecond × max(0, waited), MaxSkillTolerance)`.
- `IsModeAllowed` / `IsRegionAllowed` return `false` for empty input.
- `OnValidate()` (private, runs in the editor) clamps
  `MaxPlayersPerMatch` to 2–64, `MinPlayersPerMatch` to 2–
  `MaxPlayersPerMatch`, both timeouts to a minimum of 5 s, both skill
  values to a minimum of 0, and `MaxSkillTolerance` to at least
  `SkillTolerance`; it also repairs empty mode/region lists and defaults
  that are not in the allowed lists.

### 3.4 IMatchmakingBackend + ITickableBackend (`Runtime/IMatchmakingBackend.cs`)

```csharp
public interface ITickableBackend
{
    void Tick(float deltaTime);
}

public interface IMatchmakingBackend
{
    event Action<Lobby>             OnMatchFound;
    event Action<PlayerInfo>        OnQueueTimeout;
    event Action<PlayerInfo>        OnQueueJoined;
    event Action<PlayerInfo>        OnQueueLeft;
    event Action<Lobby>             OnReadyCheckStarted;
    event Action<Lobby>             OnReadyCheckCompleted;
    event Action<Lobby, PlayerInfo> OnReadyCheckFailed;
    event Action<Lobby>             OnLobbyUpdated;

    bool  JoinQueue(PlayerInfo player, string mode, string region);
    bool  LeaveQueue(string playerId);
    bool  CancelQueue(string playerId);
    bool  AcceptReadyCheck(string playerId);
    bool  DeclineReadyCheck(string playerId);
    Lobby CreateLobby(PlayerInfo host, string mode, string region, int maxPlayers);
    bool  JoinLobby(string lobbyId, PlayerInfo player);
    bool  LeaveLobby(string lobbyId, string playerId);
    Lobby GetLobby(string lobbyId);
    int   QueuedPlayerCount { get; }
}
```

Event meanings:

| Event | Fires when |
| --- | --- |
| `OnMatchFound` | A match is confirmed and ready to play (ready-check passed, or ready-check disabled). |
| `OnQueueTimeout` | A player waited longer than the queue timeout and was removed. |
| `OnQueueJoined` | A player entered the queue (including re-queue after a failed ready-check). |
| `OnQueueLeft` | A player left the queue by request, not by timeout. |
| `OnReadyCheckStarted` | A match was proposed; every player must now accept or decline. |
| `OnReadyCheckCompleted` | Every player accepted; `OnMatchFound` follows. |
| `OnReadyCheckFailed` | A player declined or the ready-check timed out; the lobby was dissolved. The second argument is the player who caused the failure (may be null on timeout with no missing player found). |
| `OnLobbyUpdated` | A lobby's membership or state changed. |

`CancelQueue` has the same effect as `LeaveQueue`; it exists as a named
operation because UI code reads better with it.

### 3.5 MockMatchmakingBackend (`Runtime/MockMatchmakingBackend.cs`)

```csharp
public class MockMatchmakingBackend : IMatchmakingBackend, ITickableBackend
{
    public MockMatchmakingBackend(MatchmakingConfig config);
    // All IMatchmakingBackend members, plus:
    public void Tick(float deltaTime);
    public int QueuedPlayerCount { get; } // _queue.Count
}
```

Local-only, in-memory behaviour (this is the whole backend — there is no
server, no persistence, no network call anywhere in it):

- `JoinQueue` returns `false` for a null player, a disallowed mode or
  region, a player already queued, or a player currently in a
  ready-check. Empty mode/region fall back to the config defaults.
- `LeaveQueue` / `CancelQueue` return `false` when the player is not
  queued. `CancelQueue` simply calls `LeaveQueue`.
- `CreateLobby` returns `null` for a null host, a disallowed mode or
  region, a `maxPlayers` outside `MinPlayersPerMatch`–
  `MaxPlayersPerMatch`, or a host who is currently queued.
- `JoinLobby` returns `false` for an unknown lobby id or when
  `Lobby.TryAddPlayer` refuses (full, closed, in-match, duplicate).
- `LeaveLobby` returns `false` for an unknown lobby or a player not in
  it. An emptied lobby is removed from the backend's dictionary.
- `GetLobby` returns `null` for an empty id or an unknown lobby.
- `Tick(deltaTime)` does nothing when `deltaTime <= 0`. Otherwise, in
  order: expire queue timeouts, expire ready-checks, then try to form
  matches. `MatchmakingService.Update()` calls `Tick(Time.deltaTime)`
  every frame when the backend implements `ITickableBackend`.
- A declined or timed-out ready-check dissolves the lobby and re-queues
  the players who had accepted (with their wait time reset to 0); the
  decliner is not re-queued.
- Matching groups players by mode + region, groups them into parties
  (same `PartyId` = one unit; solo = one-person party), sorts parties by
  average rating, and greedily fills a lobby up to `MaxPlayersPerMatch`
  without splitting a party. See §5 for the full algorithm.

### 3.6 MatchmakingService (`Runtime/MatchmakingService.cs`)

The only class game code normally needs. A `MonoBehaviour`.

```csharp
public class MatchmakingService : MonoBehaviour
{
    public MatchmakingConfig Config { get; }
    public IMatchmakingBackend Backend { get; private set; }
    public bool IsInitialized { get; }        // Backend != null
    public bool IsInQueue { get; private set; }
    public Lobby CurrentLobby { get; private set; }
    public PlayerInfo LocalPlayer { get; set; }

    public event Action<Lobby>             MatchFound;
    public event Action<PlayerInfo>        QueueTimedOut;
    public event Action<Lobby>             ReadyCheckStarted;
    public event Action<Lobby>             ReadyCheckCompleted;
    public event Action<Lobby, PlayerInfo> ReadyCheckFailed;

    public void   Initialize();
    public void   SetBackend(IMatchmakingBackend backend);
    public bool   JoinQueue(string mode = null, string region = null);
    public bool   JoinQueue(PlayerInfo player, string mode = null, string region = null);
    public bool   LeaveQueue();
    public bool   CancelQueue();
    public bool   AcceptReadyCheck();
    public bool   DeclineReadyCheck();
    public Lobby  CreateLobby(string mode = null, string region = null, int maxPlayers = 0);
    public bool   JoinLobby(string lobbyId);
    public bool   LeaveLobby();
}
```

Behaviour notes, exactly as implemented:

- `Awake()` calls `Initialize()`. `Update()` ticks the backend when it
  implements `ITickableBackend`. `OnDestroy()` unsubscribes from the
  backend.
- `Initialize()` does nothing if a backend already exists. With no
  config assigned it logs a warning and creates a default config in
  memory, then creates a `MockMatchmakingBackend` from the config. It is
  safe to call again after `SetBackend(...)`.
- `SetBackend(backend)` throws `ArgumentNullException` for a null
  backend, unsubscribes from the old backend first, then subscribes to
  the new one.
- `JoinQueue(mode, region)` queues `LocalPlayer`; returns `false` with
  a warning when no `LocalPlayer` is set. `JoinQueue(player, mode,
  region)` queues any player (bots, tests); `IsInQueue` only updates
  when the queued player is `LocalPlayer`. Empty mode/region fall back
  to `DefaultMode` / `DefaultRegion`.
- `LeaveQueue()` / `CancelQueue()` return `false` when not initialised
  or no `LocalPlayer` is set; on success they clear `IsInQueue`.
- `AcceptReadyCheck()` / `DeclineReadyCheck()` act on `LocalPlayer` and
  return `false` when not initialised or no `LocalPlayer` is set.
- `CreateLobby(mode, region, maxPlayers)` uses `DefaultMode` /
  `DefaultRegion` / `MaxPlayersPerMatch` for empty arguments, returns
  `null` when not initialised or no `LocalPlayer` is set, and sets
  `CurrentLobby` on success.
- `JoinLobby(lobbyId)` refreshes `CurrentLobby` from
  `Backend.GetLobby(lobbyId)` on success. `LeaveLobby()` uses
  `CurrentLobby.Id` and clears `CurrentLobby` on success; it returns
  `false` when there is no current lobby.
- Event forwarding: the service subscribes to only five backend events —
  `OnMatchFound`, `OnQueueTimeout`, `OnReadyCheckStarted`,
  `OnReadyCheckCompleted`, `OnReadyCheckFailed` — and mirrors them to
  its C# events (and, for the first four, to the Inspector UnityEvents).
  The backend events `OnQueueJoined`, `OnQueueLeft` and `OnLobbyUpdated`
  are **not** forwarded by the service. If your UI needs them, subscribe
  to `service.Backend.OnQueueJoined` (etc.) directly — but remember the
  backend instance changes when you call `SetBackend(...)`, so
  re-subscribe after a swap.
- `ReadyCheckFailed` has a C# event only — there is no matching
  Inspector UnityEvent field for it. Subscribe in code.
- `HandleMatchFound` clears `IsInQueue` and sets `CurrentLobby` only
  when the local player is in the lobby. `HandleReadyCheckFailed`
  clears `CurrentLobby` only when it is the failed lobby.

### 3.7 Editor support (`Editor/`)

Not runtime API, but part of the package:

- `MatchmakingMenu.CreateMatchmakingConfig()` —
  `Sdx17 > Matchmaking > Create Matchmaking Config`. Creates a
  `MatchmakingConfig` asset.
- `MatchmakingMenu.AddMatchmakingServiceToScene()` —
  `Sdx17 > Matchmaking > Add Matchmaking Service to Scene`. Adds a
  GameObject with `MatchmakingService` to the open scene.
- `MatchmakingConfigEditor : UnityEditor.Editor` — custom inspector for
  `MatchmakingConfig` with validation messages (`OnInspectorGUI`).

### 3.8 What is NOT in the public API

There is no method to list all lobbies, list queued players, ban or kick
a player from matchmaking, change a player's rating from the service, or
force-start a match. If you need any of those, build them on top of
`IMatchmakingBackend` / your own backend — do not assume they exist.

---

## 4. Config reference

All fields live on the `MatchmakingConfig` ScriptableObject. Defaults in
brackets are the values in the code (`MatchmakingConfig.cs`).

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `MaxPlayersPerMatch` | int | `4` | Players per lobby. Parties are never split to fit. Clamped 2–64. |
| `MinPlayersPerMatch` | int | `2` | Minimum players before a match can form. Clamped 2–`MaxPlayersPerMatch`. |
| `AllowedModes` | string[] | `duel, team, ffa` | Modes the backend accepts. Empty list is repaired to `duel`. |
| `AllowedRegions` | string[] | `eu-west, eu-east, na-east` | Regions the backend accepts. Empty list is repaired to `eu-west`. |
| `DefaultMode` | string | `duel` | Used when `JoinQueue()` / `CreateLobby()` is called with no mode. Repaired to `AllowedModes[0]` if not allowed. |
| `DefaultRegion` | string | `eu-west` | Used when called with no region. Repaired to `AllowedRegions[0]` if not allowed. |
| `QueueTimeoutSeconds` | float | `60` | A player waiting longer is removed; `OnQueueTimeout` fires. Minimum 5. |
| `ReadyCheckTimeoutSeconds` | float | `15` | Time for all players to accept a proposed match. Minimum 5. |
| `SkillTolerance` | float | `100` | Max rating difference for a match at queue time. Minimum 0. |
| `SkillToleranceGrowthPerSecond` | float | `5` | Extra tolerance per second the oldest waiter has waited. Minimum 0. |
| `MaxSkillTolerance` | float | `400` | Cap on the widened tolerance. At least `SkillTolerance`. |
| `EnableReadyCheck` | bool | `true` | Off = matches start immediately when formed. |

Worked example — tolerance over time with defaults:
`ToleranceForWait(0) = 100`, `ToleranceForWait(10) = 150`,
`ToleranceForWait(60) = 400` (capped; uncapped it would be 400 anyway at
60 s: 100 + 5×60 = 400). A 4-player group whose ratings span more than
the current tolerance does **not** match yet — it waits, and the
tolerance widens, until either the spread fits or the oldest player hits
the 60 s queue timeout.

Tuning advice:

- **Small local tests / demo:** keep the defaults. 2–4 players, short
  timeouts, tight tolerance.
- **Real play:** raise `MaxPlayersPerMatch` to your real match size,
  raise `QueueTimeoutSeconds` (60 s is short for a live game), and set
  `AllowedModes` / `AllowedRegions` to exactly what your game offers —
  every extra mode/region splits the queue and slows matching.
- **Skill tolerance:** start tight (`SkillTolerance` 100) and let the
  growth do the work. If matches take too long, raise the growth, not
  the base tolerance — otherwise new players get unfair matches
  immediately.

---

## 5. The matching algorithm, step by step

This is what `MockMatchmakingBackend.Tick` → `TryFormMatches` →
`TryFormMatchInGroup` actually does, once per frame:

1. **Timeouts first.** Queued players whose `WaitedSeconds` reached
   `QueueTimeoutSeconds` are removed (`OnQueueTimeout`). Ready-checks
   whose `RemainingSeconds` reached 0 fail (`OnReadyCheckFailed`,
   accepters re-queued).
2. **Group by mode + region.** The queue is split into groups keyed by
   `mode|region`. Matching never crosses a group.
3. **Group into parties.** Within a group, entries sharing a `PartyId`
   become one unit; solo players are one-person parties. Parties are
   sorted by **average** rating (ascending).
4. **Greedy fill.** Parties are taken in rating order, whole parties
   only, until the lobby would exceed `MaxPlayersPerMatch` or is full.
   A party that does not fit is skipped, not split.
5. **Minimum check.** If fewer than `MinPlayersPerMatch` were picked, no
   match forms this tick.
6. **Skill-spread check.** The tolerance in effect is
   `ToleranceForWait(oldestWait)` — the longest wait among the picked
   players. If `max rating − min rating` across the picked players is
   greater than that tolerance, no match forms this tick; the players
   keep waiting and the tolerance widens.
7. **Form the lobby.** The picked players leave the queue, a `Lobby` is
   created with `MaxPlayersPerMatch` slots and the first picked player
   as host, and then either a ready-check starts (`EnableReadyCheck`)
   or the match is confirmed immediately (`OnMatchFound`).

Consequences worth knowing:

- A party larger than `MaxPlayersPerMatch` can never match — it is
  skipped on every tick until its members time out.
- A single very high- or low-rated player can block a group until the
  tolerance widens enough or they time out.
- Matching is greedy by average rating, not optimal: it does not search
  for the fairest possible split, it takes parties in order until full.

---

## 6. Integration with the other Sdx17 systems

Sdx17 Matchmaking is one of 12 Sdx17 Unity systems. It is deliberately
narrow: it owns queue → lobby → ready-check → match-found, and nothing
else. Each integration below is a suggestion for **your** glue code —
none of it is built into this package, and the other systems are separate
packages with their own versions and (in v0.1.0) their own untested
status. Check each system's own README before assuming an API exists.

### Multiplayer Architecture — the required partner

This is the integration that matters most. When `MatchFound` /
`OnMatchFound` fires, hand `lobby.Players` (and `lobby.Id`,
`lobby.Mode`, `lobby.Region`) to your session / transport layer to
start the game — create or join the network session, spawn players,
load the game scene. Keep `MatchmakingService` on a GameObject that
lives for the whole session, next to your network manager.
Matchmaking decides **who** plays together; the multiplayer layer
decides **how** they connect. Without a multiplayer transport, a
"match found" is just a lobby object — nobody is actually connected.

### Combat

Nothing in this package touches combat. Once the match starts, your
combat systems read the lobby roster (`lobby.Players`, or your own
player list) as usual — for example to build teams, assign spawn
points, or seed a kill feed. Do not call combat code from a
matchmaking event handler beyond handing over the roster.

### Admin — kick

There is no kick, ban or block API in matchmaking v0.1.0 (§3.8). If an
admin kicks a player mid-match, that is your Admin system's job: remove
the player from your game session and, if you want them out of
matchmaking too, call `LeaveQueue()` / `LeaveLobby()` for them (note
those service methods act on `LocalPlayer` — for another player, call
the backend directly: `Backend.LeaveQueue(playerId)` /
`Backend.LeaveLobby(lobbyId, playerId)`). For a real game, kicking must
also be enforced server-side in your real backend — a client-side kick
alone is not anti-cheat.

### UI

Matchmaking is UI-agnostic: it exposes C# events and four Inspector
UnityEvents, and your UI subscribes to them. Minimum UI you need:
a queue button + waiting indicator, a ready-check dialog with a
countdown (`ReadyCheckTimeoutSeconds`) and Accept / Decline buttons, a
"match found" transition, and a timeout / declined message. Show the
queue position honestly — the package has no "position in queue" API,
so do not invent one; show elapsed wait time instead. The demo
(`DemoMatchmaking`, §8) shows the same flow with on-screen `OnGUI`
buttons if you want a starting point.

### Inventory

No direct coupling. The usual flow is: a match ends → your game awards
items / currency → your Inventory system grants them. Matchmaking only
supplies the roster and the result hook (`MatchFound` to start,
your own match-end event to finish). Do not grant inventory from a
matchmaking event — grant it from your game-over logic, where you know
who actually played and won.

### Quest

Same pattern as Inventory: quests observe match outcomes, they do not
drive matchmaking. On `MatchFound`, a quest system can note "match
started"; on your match-end event it can progress quests like "play 3
matches" or "win a duel". `Lobby.Mode` lets you scope quests per mode
(`duel` vs `team` vs `ffa`).

### NPC

NPCs / bots are just `PlayerInfo` objects as far as matchmaking is
concerned. The demo does exactly this: `DemoMatchmaking.AddBots()`
queues bot players through `JoinQueue(player, mode, region)` so a
match can form with one human. Give bots a stable `PlayerId`
(`"bot-1"`, …), a display name, a plausible `SkillRating`, and a
`PartyId` only if they should queue as a group. Bots never accept a
ready-check on their own in the demo — see the FAQ (§9).

### Dialogue

No direct coupling. If your game shows pre-match banter or a lobby
chat, drive it from the lobby state (`LobbyState.Waiting` /
`ReadyCheck`), not from matchmaking internals. Keep dialogue out of
the ready-check path: the ready-check timer does not wait for dialogue.

### Secure Save

Persist what matchmaking does not: the player's `SkillRating` after
`ApplyRatingDelta(...)`, their preferred mode/region, and any party /
friend data. Load the saved rating into `LocalPlayer` **before**
`JoinQueue()` so the player is matched at their real skill. Do not
trust a client-side saved rating as authoritative in a real game —
your server backend should own ratings at launch (see §7).

### Terrain Tools

No direct coupling. The link is indirect and through the Multiplayer
system: once a match is found, your game loads the map / terrain for
`lobby.Mode` (and `lobby.Region`, if you run regional servers). A
common pattern is one scene or terrain preset per mode — pick it from
`lobby.Mode` in your `MatchFound` handler.

### Developer Plugin

Use it the way you would for any system: the matchmaking service and
its config are ordinary Unity objects, so your developer console /
debug tools can inspect `service.IsInQueue`, `service.CurrentLobby`,
`service.Backend.QueuedPlayerCount`, and call `JoinQueue` /
`LeaveQueue` for testing. The Play-mode self-test
(`MatchmakingSelfTest`, §8) is the built-in diagnostic — run it before
filing a bug against matchmaking.

---

## 7. Swapping in a real backend

The mock backend is local-only. For launch you need a real backend
that implements `IMatchmakingBackend` (and `ITickableBackend` if it
needs per-frame work — a server backend can also ignore `Tick` or
drive it from its own loop). Steps:

1. Implement `IMatchmakingBackend` against your server (REST, WebSocket,
   or a dedicated matchmaking service). Implement all 8 events and all
   9 methods + `QueuedPlayerCount` — the interface is the contract;
   a partial implementation will not compile, which is intentional.
2. Keep the semantics: a `bool` return means "request accepted", the
   event is the source of truth; `OnReadyCheckFailed` must re-queue
   accepters the way the mock does, or document clearly that yours
   does not; `GetLobby` returns `null` for unknown ids.
3. Swap it in — game code does not change:

```csharp
IMatchmakingBackend realBackend = new MyServerMatchmakingBackend(config, serverUrl);
service.SetBackend(realBackend);
```

4. Call `SetBackend` **before** anyone joins the queue. Swapping
   mid-queue drops the old backend's state (the service unsubscribes
   from the old backend; the old queue is gone).
5. If your UI subscribed directly to backend events (`OnQueueJoined`,
   `OnQueueLeft`, `OnLobbyUpdated` — the ones the service does not
   forward), re-subscribe after the swap: the backend instance changed.
6. Your server owns ratings, parties, regions and anti-cheat at launch.
   Do not trust `SkillRating`, `PartyId` or `Region` values sent by a
   client.

---

## 8. Demo scene & self-test

### Demo (`Samples~/Demo/`)

No `.unity` scene file is shipped — build the scene yourself in under
5 minutes (full steps in `Samples~/Demo/README.md`):

1. New empty scene. Empty GameObject `MatchmakingService` +
   `MatchmakingService` component (or the menu item).
2. `Sdx17 > Matchmaking > Create Matchmaking Config`, drag it into the
   service's **Config** field.
3. Empty GameObject `Demo` + `DemoMatchmaking` component; drag the
   service into its `Matchmaking Service` field (it also finds the
   service automatically). Optional: set `Bot Count` to 3 — the default
   config makes 4-player matches, so 1 local player + 3 bots fills a
   lobby.
4. Press Play → **Join Queue** → **Add Bots (so a match can form)** →
   ready-check starts → **Accept Ready-Check**.

`DemoMatchmaking` public methods: `JoinQueue()`, `AddBots()`,
`AcceptReadyCheck()`, `DeclineReadyCheck()`, `LeaveQueue()`.
**Known demo behaviour:** bots never accept a ready-check on their
own, so in the demo the match only completes when every player
accepts — with bots present, accept as the local player and watch the
ready-check time out, or use the self-test for the full flow.

### Self-test (`Tests~/Runtime/MatchmakingSelfTest.cs`)

Attach `MatchmakingSelfTest` to a GameObject in a test scene and press
Play. It drives the backend directly, accepts every ready-check, and
logs PASS/FAIL lines to the Console, ending with
`DONE: 34 passed, 0 failed` when everything passes (34 checks cover
queueing, duplicates, invalid mode/region, match formation, skill-spread
rejection, parties, ready-check accept/decline, timeout, and lobby
create/join/leave — per the README). If any check fails, fix that path
before shipping; do not ship with a failing self-test.

---

## 9. Troubleshooting / FAQ

**Q1. I call `JoinQueue()` and it returns `false`. Why?**
The common causes, in order: no `LocalPlayer` is set (the service logs
a warning — set `LocalPlayer` before queueing); the player is already
queued (duplicate joins are rejected); the player is currently in a
ready-check; or the mode/region is not in `AllowedModes` /
`AllowedRegions` (check the config — `JoinQueue()` with no arguments
uses `DefaultMode` / `DefaultRegion`). `false` only means the request
was rejected; check `IsInQueue` and the Console warning to see which.

**Q2. Nobody ever matches — the queue just sits there. What's wrong?**
Check four things: (a) are there at least `MinPlayersPerMatch` (default
2) players queued in the **same** mode and region? Queueing across
modes or regions never mixes. (b) Is the skill spread wider than the
current tolerance? With defaults, four players spanning more than 100
rating points do not match at queue time — they wait for the tolerance
to widen (up to 400). (c) Is a party larger than `MaxPlayersPerMatch`
in the queue? It can never match. (d) Is `MinPlayersPerMatch` higher
than the number of players you have? In the demo, remember to click
**Add Bots** — one local player alone never forms a match.

**Q3. The ready-check starts but the match never completes in the demo.**
That is expected demo behaviour, not a bug: bots never accept a
ready-check on their own, and the match only completes when **every**
player accepts. Accept as the local player and watch the ready-check
time out (15 s by default), or run the self-test, which accepts every
ready-check and logs the full flow. In your own game, every real
player must click Accept — design the ready-check UI so that is
obvious, with a visible countdown.

**Q4. A player declined the ready-check and everyone was kicked out.
Is that a bug?**
No — that is the designed flow. A decline (or a ready-check timeout)
dissolves the lobby, and the players who had already accepted are
re-queued automatically; only the decliner is not. Your UI should say
so ("A player declined — finding a new match…") instead of showing a
generic error. If `ReadyCheckFailed` fires, check its second argument
(`cause`) — it is the player who declined or timed out.

**Q5. Can I use this for a real launched game — my players are on
different devices?**
Not with the included backend. The mock backend is **local-only**: it
keeps one queue in memory, in one Unity process, with no server, no
persistence, no anti-cheat and no cross-device play. Every player must
be in the same Unity process. For launch you need a real backend
implementing `IMatchmakingBackend` (§7) plus a multiplayer transport
to actually connect the matched players. Shipping the mock backend as
if it were production matchmaking will not work — two devices each run
their own empty queue and will never find each other.

**Q6. I subscribed to `OnQueueJoined` / `OnLobbyUpdated` on the
service and nothing fires. Why?**
Those are backend events, and the service forwards only five of them
(`OnMatchFound`, `OnQueueTimeout`, `OnReadyCheckStarted`,
`OnReadyCheckCompleted`, `OnReadyCheckFailed`). `OnQueueJoined`,
`OnQueueLeft` and `OnLobbyUpdated` are not forwarded. Subscribe to
them on `service.Backend` directly — and re-subscribe after any
`SetBackend(...)` call, because the backend instance changes. The
same applies to `ReadyCheckFailed`: it has a C# event only, no
Inspector UnityEvent — subscribe in code.

**Q7. `CreateLobby` returns `null` / `JoinLobby` returns `false`.**
`CreateLobby` returns `null` when there is no `LocalPlayer`, the mode
or region is not allowed, `maxPlayers` is outside
`MinPlayersPerMatch`–`MaxPlayersPerMatch`, or the host is currently
queued (leave the queue first). `JoinLobby` returns `false` for an
unknown lobby id, or when `TryAddPlayer` refuses — lobby full, closed,
in-match, or the player is already in it. Remember a lobby id is a
GUID string: copy it exactly, do not retype it from memory.

**Q8. The queue timed out after 60 seconds with one player waiting.
Is 60 seconds a bug?**
No — that is `QueueTimeoutSeconds` (default 60). A player waiting
longer is removed and `OnQueueTimeout` fires. For a live game, raise
the timeout in the config; for local testing, 60 s is deliberately
short so you can see the timeout fire. Leave the queue running with
one player for 60 seconds to verify your timeout UI works.

**Q9. I changed the config in the Inspector but matching did not change.**
Check that the service's **Config** field points at the asset you
edited (the service warns and uses in-memory defaults when nothing is
assigned), and that you edited the asset, not a scene copy. Config
values are read from the assigned `MatchmakingConfig` at runtime; the
mock backend holds a reference to that asset, so edits apply to the
next match attempt — but players already queued keep the mode/region
they queued with.

**Q10. I get compile errors after importing. What do I check first?**
Confirm you are on **Unity 2022.3 LTS** (the package targets
`unity: 2022.3` in `package.json`), that the package is installed as a
whole (all of `Runtime/`, including the `.asmdef`), and that your code
has `using Sdx17.Matchmaking;`. If the errors mention the Editor
folder, check you did not copy `Editor/` into a runtime-only assembly.
And remember the honest status of v0.1.0: it has **not been compiled
in Unity yet** — if you hit a compile error, it may be a real bug in
this draft. Report it with the exact error, your Unity version, and
the steps to reproduce (see LISTING.md for the support contact).

---

## 10. Performance notes

Honest note first: **no performance measurements have been taken** —
v0.1.0 is untested in Unity, so everything below is reasoning from the
code, not benchmarks. Measure in your own project before making
performance claims.

- **Per-frame cost is small and proportional to the queue.** `Tick`
  runs once per frame from `MatchmakingService.Update()`. It iterates
  the queue (timeouts), the active ready-checks, and then groups the
  queue by mode + region and sorts parties by average rating. For a
  local mock backend with tens of players this is trivial; the sort is
  the only non-linear step, and it runs over parties, not individual
  players.
- **Allocations:** `Tick` allocates per frame — group dictionaries,
  party lists, and (in `ExpireReadyChecks`) a copied list of the
  active checks. In the mock backend this is fine for local testing;
  do not use the mock backend as a performance reference for a server
  backend, and do not run it with thousands of queued players.
- **No networking, no persistence, no background threads.** The mock
  backend does everything on the main thread, in `Update`. There is
  nothing to profile on the network side because there is no network.
- **Lobby / PlayerInfo are plain C# objects.** No per-player
  `MonoBehaviour`, no per-player GameObject. The only `MonoBehaviour`
  is `MatchmakingService` itself, plus the demo and self-test scripts.
- **If you build a real backend:** keep the per-frame work server-side
  or event-driven; do not port the mock's per-frame dictionary
  allocations to a server handling thousands of concurrent players.
  Profile `QueuedPlayerCount` growth under load and set a sane
  `QueueTimeoutSeconds` so a dead queue cannot grow unboundedly
  (the mock removes timed-out players, but a server backend must do
  the same).

---

## 11. Limitations (read before you buy / ship)

- **UNTESTED IN UNITY (v0.1.0).** Nothing here has been compiled or
  run inside Unity yet. There may be compile errors, and the demo and
  self-test have never been observed passing. Run the README test
  checklist in Unity 2022.3 LTS before relying on any of it.
- **Mock backend is local-only.** No server, no persistence, no
  anti-cheat, no cross-device play. Every player must be in the same
  Unity process. A real backend implementing `IMatchmakingBackend` is
  required before launch (§7).
- **Not a networking layer.** Matchmaking decides who plays together;
  your multiplayer transport decides how they connect. This package
  contains no networking code at all.
- **No rating updates, no persistence.** `SkillRating` is a plain
  number. The package never changes it after a match and never saves
  it — you call `ApplyRatingDelta(...)` and persist it yourself.
- **No kick / ban / block API.** No way to list all lobbies or queued
  players, no admin controls inside matchmaking (§3.8). Build those
  in your backend / Admin system.
- **Greedy matching, not optimal.** Parties are taken in average-rating
  order until the lobby is full; the result is fair-ish, not the
  fairest possible split. A party larger than `MaxPlayersPerMatch` can
  never match, and a single outlier rating can stall a group.
- **No "position in queue" API.** Do not show a queue position you
  cannot measure; show elapsed wait time instead.
- **Ready-check failure has no Inspector UnityEvent.** Only the C#
  event `ReadyCheckFailed` exists — subscribe in code.
- **Backend-only events are not forwarded by the service.**
  `OnQueueJoined`, `OnQueueLeft` and `OnLobbyUpdated` must be
  subscribed on `service.Backend` directly, and re-subscribed after
  `SetBackend(...)`.
- **Demo limitations.** No `.unity` scene file is shipped; you build
  the scene from the steps in `Samples~/Demo/README.md`. Demo bots
  never accept a ready-check, so the demo alone cannot complete a
  match — use the self-test for the full flow.
- **No sales, reviews or Asset Store listing exist yet.** This is a
  first release by a new publisher. The price reflects the scope of
  what is in the package, not a track record.

---

*Sdx17 Matchmaking v0.1.0 — Guide. MIT licensed. See `LICENSE`,
`README.md`, `CHANGELOG.md` and `LISTING.md` in the package root.*
