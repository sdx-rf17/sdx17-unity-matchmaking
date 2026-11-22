using UnityEngine;

namespace Sdx17.Matchmaking.Tests
{
    /// <summary>
    /// Play-mode self-test for the matchmaking core. Attach to a GameObject in a
    /// test scene and press Play. It drives a <see cref="MockMatchmakingBackend"/>
    /// directly (no service, no UI) and logs one PASS/FAIL line per check plus a
    /// summary. It does not need a network connection or any other Sdx17 package.
    ///
    /// Checks: queue join + duplicate rejection, invalid mode rejection, match
    /// formation within skill tolerance, party kept together, ready-check accept
    /// flow, ready-check decline dissolves the lobby and re-queues accepters,
    /// queue timeout, lobby create/join/leave, and skill-spread rejection.
    /// </summary>
    public class MatchmakingSelfTest : MonoBehaviour
    {
        private int _passed;
        private int _failed;

        private void Start()
        {
            Debug.Log("[SelfTest] Sdx17 Matchmaking self-test starting...");
            RunAll();
            Debug.Log($"[SelfTest] DONE: {_passed} passed, {_failed} failed.");
            if (_failed > 0)
                Debug.LogError("[SelfTest] FAILURES PRESENT — see FAIL lines above.");
        }

        private void Check(string name, bool condition)
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"[SelfTest] PASS: {name}");
            }
            else
            {
                _failed++;
                Debug.LogError($"[SelfTest] FAIL: {name}");
            }
        }

        private static MatchmakingConfig NewConfig()
        {
            var config = ScriptableObject.CreateInstance<MatchmakingConfig>();
            return config;
        }

        private static PlayerInfo Player(string id, float rating, string partyId = null)
        {
            return new PlayerInfo(id, id, rating, "eu-west", partyId);
        }

        private void RunAll()
        {
            TestQueueJoinAndDuplicate();
            TestInvalidModeRejected();
            TestMatchFormsWithinTolerance();
            TestSkillSpreadRejected();
            TestPartyKeptTogether();
            TestReadyCheckAcceptFlow();
            TestReadyCheckDeclineRequeuesAccepters();
            TestQueueTimeout();
            TestLobbyCreateJoinLeave();
        }

        private void TestQueueJoinAndDuplicate()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            var player = Player("p1", 1000f);
            Check("queue: first join accepted", backend.JoinQueue(player, "duel", "eu-west"));
            Check("queue: duplicate join rejected", !backend.JoinQueue(player, "duel", "eu-west"));
            Check("queue: count is 1", backend.QueuedPlayerCount == 1);
            Check("queue: leave works", backend.LeaveQueue("p1"));
            Check("queue: count is 0 after leave", backend.QueuedPlayerCount == 0);
            Check("queue: second leave rejected", !backend.LeaveQueue("p1"));
        }

        private void TestInvalidModeRejected()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            var player = Player("p1", 1000f);
            Check("queue: unknown mode rejected", !backend.JoinQueue(player, "no-such-mode", "eu-west"));
            Check("queue: unknown region rejected", !backend.JoinQueue(player, "duel", "no-such-region"));
        }

        private void TestMatchFormsWithinTolerance()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            Lobby found = null;
            backend.OnMatchFound += lobby => found = lobby;
            backend.OnReadyCheckStarted += lobby =>
            {
                foreach (PlayerInfo p in lobby.Players)
                    backend.AcceptReadyCheck(p.PlayerId);
            };

            backend.JoinQueue(Player("a", 1000f), "duel", "eu-west");
            backend.JoinQueue(Player("b", 1010f), "duel", "eu-west");
            backend.JoinQueue(Player("c", 1020f), "duel", "eu-west");
            backend.JoinQueue(Player("d", 1030f), "duel", "eu-west");
            backend.Tick(0.5f);

            Check("match: forms with 4 close-rated players", found != null);
            Check("match: lobby has 4 players", found != null && found.PlayerCount == 4);
            Check("match: lobby is InMatch", found != null && found.State == LobbyState.InMatch);
            Check("match: queue is empty afterwards", backend.QueuedPlayerCount == 0);
        }

        private void TestSkillSpreadRejected()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            Lobby found = null;
            backend.OnMatchFound += lobby => found = lobby;

            backend.JoinQueue(Player("a", 1000f), "duel", "eu-west");
            backend.JoinQueue(Player("b", 2000f), "duel", "eu-west");
            backend.JoinQueue(Player("c", 1000f), "duel", "eu-west");
            backend.JoinQueue(Player("d", 2000f), "duel", "eu-west");
            backend.Tick(0.5f);

            Check("match: wide skill spread does not match immediately", found == null);
        }

        private void TestPartyKeptTogether()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            Lobby found = null;
            backend.OnMatchFound += lobby => found = lobby;
            backend.OnReadyCheckStarted += lobby =>
            {
                foreach (PlayerInfo p in lobby.Players)
                    backend.AcceptReadyCheck(p.PlayerId);
            };

            backend.JoinQueue(Player("a", 1000f, "party-1"), "duel", "eu-west");
            backend.JoinQueue(Player("b", 1005f, "party-1"), "duel", "eu-west");
            backend.JoinQueue(Player("c", 1010f), "duel", "eu-west");
            backend.JoinQueue(Player("d", 1015f), "duel", "eu-west");
            backend.Tick(0.5f);

            bool partyTogether = found != null
                && found.ContainsPlayer("a")
                && found.ContainsPlayer("b");
            Check("match: party members end up in the same lobby", partyTogether);
        }

        private void TestReadyCheckAcceptFlow()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            Lobby proposed = null;
            Lobby confirmed = null;
            backend.OnReadyCheckStarted += lobby => proposed = lobby;
            backend.OnMatchFound += lobby => confirmed = lobby;

            backend.JoinQueue(Player("a", 1000f), "duel", "eu-west");
            backend.JoinQueue(Player("b", 1001f), "duel", "eu-west");
            backend.JoinQueue(Player("c", 1002f), "duel", "eu-west");
            backend.JoinQueue(Player("d", 1003f), "duel", "eu-west");
            backend.Tick(0.5f);

            Check("ready-check: starts when a match is proposed", proposed != null);
            Check("ready-check: match NOT confirmed before accepts", confirmed == null);
            Check("ready-check: lobby is in ReadyCheck state", proposed != null && proposed.State == LobbyState.ReadyCheck);

            backend.AcceptReadyCheck("a");
            backend.AcceptReadyCheck("b");
            backend.AcceptReadyCheck("c");
            Check("ready-check: still waiting after 3 of 4 accepts", confirmed == null);
            backend.AcceptReadyCheck("d");
            Check("ready-check: match confirmed after all accepts", confirmed != null);
        }

        private void TestReadyCheckDeclineRequeuesAccepters()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            Lobby failedLobby = null;
            backend.OnReadyCheckFailed += (lobby, cause) => failedLobby = lobby;

            backend.JoinQueue(Player("a", 1000f), "duel", "eu-west");
            backend.JoinQueue(Player("b", 1001f), "duel", "eu-west");
            backend.JoinQueue(Player("c", 1002f), "duel", "eu-west");
            backend.JoinQueue(Player("d", 1003f), "duel", "eu-west");
            backend.Tick(0.5f);

            backend.AcceptReadyCheck("a");
            backend.AcceptReadyCheck("b");
            backend.DeclineReadyCheck("c");

            Check("ready-check: decline dissolves the lobby", failedLobby != null);
            Check("ready-check: dissolved lobby is Closed", failedLobby != null && failedLobby.State == LobbyState.Closed);
            Check("ready-check: accepters are re-queued (2 players)", backend.QueuedPlayerCount == 2);
            Check("ready-check: decliner is NOT re-queued", backend.GetLobby(failedLobby.Id) == null);
        }

        private void TestQueueTimeout()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            PlayerInfo timedOut = null;
            backend.OnQueueTimeout += player => timedOut = player;

            backend.JoinQueue(Player("solo", 1000f), "duel", "eu-west");
            backend.Tick(30f);
            Check("timeout: player still queued at 30s", timedOut == null && backend.QueuedPlayerCount == 1);
            backend.Tick(31f);
            Check("timeout: player removed after 60s", timedOut != null && timedOut.PlayerId == "solo");
            Check("timeout: queue empty after timeout", backend.QueuedPlayerCount == 0);
        }

        private void TestLobbyCreateJoinLeave()
        {
            var backend = new MockMatchmakingBackend(NewConfig());
            var host = Player("host", 1000f);
            Lobby lobby = backend.CreateLobby(host, "duel", "eu-west", 4);
            Check("lobby: create returns a lobby", lobby != null);
            Check("lobby: host is in it", lobby != null && lobby.ContainsPlayer("host"));
            Check("lobby: second player joins", backend.JoinLobby(lobby.Id, Player("guest", 1000f)));
            Check("lobby: now has 2 players", lobby.PlayerCount == 2);
            Check("lobby: guest leaves", backend.LeaveLobby(lobby.Id, "guest"));
            Check("lobby: back to 1 player", lobby.PlayerCount == 1);
            Check("lobby: unknown lobby id rejected", !backend.JoinLobby("no-such-lobby", Player("x", 1000f)));
            Check("lobby: lookup by id works", backend.GetLobby(lobby.Id) == lobby);
        }
    }
}
