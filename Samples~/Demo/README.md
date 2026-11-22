# Demo — Sdx17 Matchmaking

Minimal demo scene setup. No `.unity` scene file is included in this package;
build the scene yourself in under 5 minutes with the steps below.

## Scene setup

1. Create a new empty scene (File > New Scene).
2. Create an empty GameObject named `MatchmakingService` and add the
   `MatchmakingService` component to it.
   (Shortcut: `Sdx17 > Matchmaking > Add Matchmaking Service to Scene`.)
3. Create the config: `Sdx17 > Matchmaking > Create Matchmaking Config`,
   then drag the created `MatchmakingConfig` asset into the service's
   `Config` field in the Inspector.
4. Create an empty GameObject named `Demo` and add the `DemoMatchmaking`
   component to it. Drag the `MatchmakingService` GameObject into the demo's
   `Matchmaking Service` field (it also finds the service automatically).
5. Optional: set `Bot Count` to 3 on the demo. The default config makes
   4-player matches, so 1 local player + 3 bots fills a lobby.

## Running it

1. Press Play.
2. Click **Join Queue**.
3. Click **Add Bots (so a match can form)**.
4. A ready-check starts within a second. Click **Accept Ready-Check**.
   (Bots never accept on their own — in this demo the match only completes
   when every player accepts, so use the self-test below for the full flow,
   or accept as the local player and watch the ready-check time out.)
5. Watch the status box and the Console. `MATCH FOUND!` means the flow works.

To see a full match complete without waiting, run the self-test instead:
add `MatchmakingSelfTest` to a GameObject in a test scene and press Play —
it drives the backend directly, accepts every ready-check, and logs
PASS/FAIL lines to the Console.

## What the demo does NOT do

- No real networking: all players (including bots) live in this one Unity
  process. Real multiplayer needs a server backend plus your multiplayer
  transport; see the main README's integration notes.
- No scene file is shipped, so nothing here can be opened by double-clicking
  — follow the steps above.
