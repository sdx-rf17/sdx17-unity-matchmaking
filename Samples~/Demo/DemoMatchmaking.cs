using System.Collections.Generic;
using UnityEngine;

namespace Sdx17.Matchmaking.Demo
{
    /// <summary>
    /// Minimal demo for the Sdx17 Matchmaking package. Attach it to a GameObject
    /// next to a <see cref="MatchmakingService"/> (see Samples~/Demo/README.md),
    /// press Play, and use the on-screen buttons: queue as the local player,
    /// add simulated bot players so a match can form locally, then accept the
    /// ready-check. Everything is logged to the Console as well.
    ///
    /// The bots exist only so a match can form with one real player in the
    /// editor. They are not part of the package runtime and must not ship.
    /// </summary>
    public class DemoMatchmaking : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchmakingService matchmakingService;

        [Header("Local player")]
        [SerializeField] private string localPlayerName = "DemoPlayer";
        [SerializeField] private float localSkillRating = 1000f;
        [SerializeField] private string mode = "duel";
        [SerializeField] private string region = "eu-west";

        [Header("Bots (local testing only)")]
        [SerializeField] private int botCount = 3;
        [SerializeField] private float botSkillRating = 1010f;

        private readonly List<PlayerInfo> _bots = new List<PlayerInfo>();
        private string _status = "Idle. Press 'Join Queue'.";

        private void Awake()
        {
            if (matchmakingService == null)
                matchmakingService = FindObjectOfType<MatchmakingService>();

            if (matchmakingService == null)
            {
                Debug.LogError("[Demo] No MatchmakingService found in the scene. Add one via Sdx17 > Matchmaking > Add Matchmaking Service to Scene.");
                enabled = false;
                return;
            }

            matchmakingService.LocalPlayer = new PlayerInfo(
                playerId: System.Guid.NewGuid().ToString("N"),
                displayName: localPlayerName,
                skillRating: localSkillRating,
                region: region);

            matchmakingService.MatchFound += OnMatchFound;
            matchmakingService.QueueTimedOut += OnQueueTimeout;
            matchmakingService.ReadyCheckStarted += OnReadyCheckStarted;
            matchmakingService.ReadyCheckCompleted += OnReadyCheckCompleted;
            matchmakingService.ReadyCheckFailed += OnReadyCheckFailed;
        }

        private void OnDestroy()
        {
            if (matchmakingService == null) return;
            matchmakingService.MatchFound -= OnMatchFound;
            matchmakingService.QueueTimedOut -= OnQueueTimeout;
            matchmakingService.ReadyCheckStarted -= OnReadyCheckStarted;
            matchmakingService.ReadyCheckCompleted -= OnReadyCheckCompleted;
            matchmakingService.ReadyCheckFailed -= OnReadyCheckFailed;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 320, 260), GUI.skin.box);
            GUILayout.Label("Sdx17 Matchmaking Demo", GUI.skin.label);
            GUILayout.Label(_status);
            GUILayout.Space(8);

            if (GUILayout.Button("Join Queue"))
                JoinQueue();
            if (GUILayout.Button("Add Bots (so a match can form)"))
                AddBots();
            if (GUILayout.Button("Accept Ready-Check"))
                AcceptReadyCheck();
            if (GUILayout.Button("Decline Ready-Check"))
                DeclineReadyCheck();
            if (GUILayout.Button("Leave Queue"))
                LeaveQueue();

            GUILayout.EndArea();
        }

        public void JoinQueue()
        {
            if (matchmakingService.JoinQueue(mode, region))
            {
                _status = $"Queued for {mode} ({region}). Waiting for players...";
                Debug.Log("[Demo] Joined queue.");
            }
            else
            {
                _status = "Join queue failed (already queued, or mode/region not allowed).";
                Debug.LogWarning("[Demo] JoinQueue returned false.");
            }
        }

        public void AddBots()
        {
            _bots.Clear();
            for (int i = 0; i < botCount; i++)
            {
                var bot = new PlayerInfo(
                    playerId: $"bot-{i}-{System.Guid.NewGuid():N}",
                    displayName: $"Bot {i + 1}",
                    skillRating: botSkillRating + i * 5f,
                    region: region);
                _bots.Add(bot);
                matchmakingService.JoinQueue(bot, mode, region);
            }
            _status = $"Added {botCount} bots to the queue.";
            Debug.Log($"[Demo] Added {botCount} bots.");
        }

        public void AcceptReadyCheck()
        {
            if (matchmakingService.AcceptReadyCheck())
            {
                _status = "Ready-check accepted. Waiting for the other players...";
                Debug.Log("[Demo] Ready-check accepted.");
            }
            else
            {
                _status = "No ready-check to accept right now.";
            }
        }

        public void DeclineReadyCheck()
        {
            if (matchmakingService.DeclineReadyCheck())
            {
                _status = "Ready-check declined. The lobby was dissolved.";
                Debug.Log("[Demo] Ready-check declined.");
            }
            else
            {
                _status = "No ready-check to decline right now.";
            }
        }

        public void LeaveQueue()
        {
            if (matchmakingService.LeaveQueue())
            {
                _status = "Left the queue.";
                Debug.Log("[Demo] Left queue.");
            }
            else
            {
                _status = "Not in the queue.";
            }
        }

        private void OnMatchFound(Lobby lobby)
        {
            _status = $"MATCH FOUND! Lobby {lobby.Id.Substring(0, 8)} with {lobby.PlayerCount} players. Game can start.";
            Debug.Log($"[Demo] Match found: {lobby.PlayerCount} players in {lobby.Mode} ({lobby.Region}).");
            foreach (PlayerInfo player in lobby.Players)
                Debug.Log($"[Demo]   - {player.DisplayName} (rating {player.SkillRating})");
        }

        private void OnQueueTimeout(PlayerInfo player)
        {
            _status = $"Queue timed out for {player.DisplayName}. Try again.";
            Debug.LogWarning($"[Demo] Queue timed out for {player.DisplayName}.");
        }

        private void OnReadyCheckStarted(Lobby lobby)
        {
            _status = $"Match proposed ({lobby.PlayerCount} players). Accept the ready-check!";
            Debug.Log($"[Demo] Ready-check started for lobby {lobby.Id.Substring(0, 8)}.");
        }

        private void OnReadyCheckCompleted(Lobby lobby)
        {
            _status = "Everyone accepted. Match starting...";
            Debug.Log("[Demo] Ready-check completed.");
        }

        private void OnReadyCheckFailed(Lobby lobby, PlayerInfo cause)
        {
            string who = cause != null ? cause.DisplayName : "a player";
            _status = $"Ready-check failed ({who} declined or timed out). Re-queued.";
            Debug.LogWarning($"[Demo] Ready-check failed because of {who}.");
        }
    }
}
