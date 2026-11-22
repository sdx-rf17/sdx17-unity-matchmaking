# Changelog

All notable changes to this package will be documented in this file.

## [0.1.0] - 2026-11-21

### Added
- `IMatchmakingBackend` interface plus `MockMatchmakingBackend` (in-memory queue for local testing only).
- `Lobby` data model with states Waiting, ReadyCheck, InMatch, Closed.
- `PlayerInfo` with skill rating, region, and party id.
- `MatchmakingConfig` ScriptableObject: match size, queue timeout, ready-check timeout, skill tolerance and growth, allowed modes and regions.
- `MatchmakingService` MonoBehaviour facade: join/leave/cancel queue, ready-check accept/decline, lobby create/join/leave, per-frame ticking of the backend.
- Skill-based matching: parties are kept together, lobbies form from players whose ratings fit inside a tolerance that widens the longer the oldest player has waited.
- Ready-check flow: all players must accept before a match is confirmed; a decline or timeout dissolves the lobby and re-queues the players who accepted.
- Queue timeout: a player waiting longer than `queueTimeoutSeconds` is removed and `OnQueueTimeout` fires.
- Editor support: custom inspector for `MatchmakingConfig` with validation, and `Sdx17 > Matchmaking > Create Matchmaking Config` menu item.
- `Samples~/Demo`: `DemoMatchmaking` script plus scene-setup README.
- `Tests~/Runtime`: `MatchmakingSelfTest` Play-mode self-test MonoBehaviour that logs PASS/FAIL for the core paths.

### Notes
- Version 0.1.0 is UNTESTED IN UNITY. No Unity editor was available in the build environment, so nothing here has been compiled or run inside Unity yet.
- The mock backend is local-only. It is not a real production matchmaker; a server backend implementing `IMatchmakingBackend` is required before launch.
