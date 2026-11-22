using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sdx17.Matchmaking
{
    /// <summary>
    /// In-memory matchmaking backend for local testing only.
    ///
    /// What it does, honestly: it keeps one queue in memory, groups players by
    /// mode + region, keeps parties together, and forms lobbies when enough
    /// players fit inside the skill tolerance. It runs ready-checks and queue
    /// timeouts on a per-frame Tick.
    ///
    /// What it is NOT: a production matchmaker. There is no server, no
    /// persistence, no anti-cheat, and no cross-device play — every player must
    /// be in the same Unity process. A real backend implementing
    /// <see cref="IMatchmakingBackend"/> is required before launch.
    ///
    /// Design notes:
    /// - Parties are matched as units: players sharing a PartyId are grouped
    ///   before matching and never split across lobbies.
    /// - Skill tolerance widens with the oldest player's wait time, so a queue
    ///   with a wide skill spread still resolves instead of timing everyone out.
    /// - A declined or timed-out ready-check dissolves the lobby and re-queues
    ///   the players who had already accepted, so one declined player does not
    ///   punish everyone else with a fresh queue from scratch.
    /// </summary>
    public class MockMatchmakingBackend : IMatchmakingBackend, ITickableBackend
    {
        public event Action<Lobby> OnMatchFound;
        public event Action<PlayerInfo> OnQueueTimeout;
        public event Action<PlayerInfo> OnQueueJoined;
        public event Action<PlayerInfo> OnQueueLeft;
        public event Action<Lobby> OnReadyCheckStarted;
        public event Action<Lobby> OnReadyCheckCompleted;
        public event Action<Lobby, PlayerInfo> OnReadyCheckFailed;
        public event Action<Lobby> OnLobbyUpdated;

        public int QueuedPlayerCount => _queue.Count;

        private readonly MatchmakingConfig _config;

        private sealed class QueueEntry
        {
            public PlayerInfo Player;
            public string Mode;
            public string Region;
            public float WaitedSeconds;
        }

        private sealed class ReadyCheck
        {
            public Lobby Lobby;
            public float RemainingSeconds;
            public HashSet<string> Accepted = new HashSet<string>();
            public HashSet<string> Declined = new HashSet<string>();
        }

        private readonly List<QueueEntry> _queue = new List<QueueEntry>();
        private readonly Dictionary<string, Lobby> _lobbies = new Dictionary<string, Lobby>();
        private readonly Dictionary<string, ReadyCheck> _readyChecksByPlayer = new Dictionary<string, ReadyCheck>();

        public MockMatchmakingBackend(MatchmakingConfig config)
        {
            _config = config != null ? config : ScriptableObject.CreateInstance<MatchmakingConfig>();
        }

        // ---------------------------------------------------------- queueing

        public bool JoinQueue(PlayerInfo player, string mode, string region)
        {
            if (player == null) return false;
            if (string.IsNullOrEmpty(mode)) mode = _config.DefaultMode;
            if (string.IsNullOrEmpty(region)) region = _config.DefaultRegion;
            if (!_config.IsModeAllowed(mode) || !_config.IsRegionAllowed(region)) return false;
            if (FindQueueEntry(player.PlayerId) != null) return false;
            if (_readyChecksByPlayer.ContainsKey(player.PlayerId)) return false;

            _queue.Add(new QueueEntry { Player = player, Mode = mode, Region = region, WaitedSeconds = 0f });
            OnQueueJoined?.Invoke(player);
            return true;
        }

        public bool LeaveQueue(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            QueueEntry entry = FindQueueEntry(playerId);
            if (entry == null) return false;

            _queue.Remove(entry);
            OnQueueLeft?.Invoke(entry.Player);
            return true;
        }

        public bool CancelQueue(string playerId)
        {
            return LeaveQueue(playerId);
        }

        // ---------------------------------------------------------- ready-check

        public bool AcceptReadyCheck(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            if (!_readyChecksByPlayer.TryGetValue(playerId, out ReadyCheck check)) return false;
            if (check.Declined.Contains(playerId)) return false;

            check.Accepted.Add(playerId);
            if (check.Accepted.Count >= check.Lobby.PlayerCount)
            {
                CompleteReadyCheck(check);
            }
            return true;
        }

        public bool DeclineReadyCheck(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            if (!_readyChecksByPlayer.TryGetValue(playerId, out ReadyCheck check)) return false;

            FailReadyCheck(check, FindPlayerInLobby(check.Lobby, playerId));
            return true;
        }

        // ---------------------------------------------------------- lobbies

        public Lobby CreateLobby(PlayerInfo host, string mode, string region, int maxPlayers)
        {
            if (host == null) return null;
            if (string.IsNullOrEmpty(mode)) mode = _config.DefaultMode;
            if (string.IsNullOrEmpty(region)) region = _config.DefaultRegion;
            if (!_config.IsModeAllowed(mode) || !_config.IsRegionAllowed(region)) return null;
            if (maxPlayers < _config.MinPlayersPerMatch || maxPlayers > _config.MaxPlayersPerMatch) return null;
            if (FindQueueEntry(host.PlayerId) != null) return null;

            var lobby = new Lobby(mode, region, maxPlayers, host);
            _lobbies.Add(lobby.Id, lobby);
            OnLobbyUpdated?.Invoke(lobby);
            return lobby;
        }

        public bool JoinLobby(string lobbyId, PlayerInfo player)
        {
            if (string.IsNullOrEmpty(lobbyId) || player == null) return false;
            if (!_lobbies.TryGetValue(lobbyId, out Lobby lobby)) return false;
            if (!lobby.TryAddPlayer(player)) return false;

            OnLobbyUpdated?.Invoke(lobby);
            return true;
        }

        public bool LeaveLobby(string lobbyId, string playerId)
        {
            if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(playerId)) return false;
            if (!_lobbies.TryGetValue(lobbyId, out Lobby lobby)) return false;
            if (!lobby.RemovePlayer(playerId)) return false;

            if (lobby.IsEmpty)
                _lobbies.Remove(lobbyId);
            OnLobbyUpdated?.Invoke(lobby);
            return true;
        }

        public Lobby GetLobby(string lobbyId)
        {
            if (string.IsNullOrEmpty(lobbyId)) return null;
            _lobbies.TryGetValue(lobbyId, out Lobby lobby);
            return lobby;
        }

        // ---------------------------------------------------------- tick

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            ExpireQueueTimeouts(deltaTime);
            ExpireReadyChecks(deltaTime);
            TryFormMatches();
        }

        private void ExpireQueueTimeouts(float deltaTime)
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                QueueEntry entry = _queue[i];
                entry.WaitedSeconds += deltaTime;
                if (entry.WaitedSeconds >= _config.QueueTimeoutSeconds)
                {
                    _queue.RemoveAt(i);
                    OnQueueTimeout?.Invoke(entry.Player);
                }
            }
        }

        private void ExpireReadyChecks(float deltaTime)
        {
            // Copy the values: failing a check mutates the dictionary.
            var checks = new List<ReadyCheck>(_readyChecksByPlayer.Values);
            var seen = new HashSet<ReadyCheck>();
            foreach (ReadyCheck check in checks)
            {
                if (!seen.Add(check)) continue;
                check.RemainingSeconds -= deltaTime;
                if (check.RemainingSeconds <= 0f)
                {
                    PlayerInfo missing = FirstNotAccepted(check);
                    FailReadyCheck(check, missing);
                }
            }
        }

        private void TryFormMatches()
        {
            if (_queue.Count < _config.MinPlayersPerMatch) return;

            // Group by mode + region first: never match across either.
            var groups = new Dictionary<string, List<QueueEntry>>();
            foreach (QueueEntry entry in _queue)
            {
                string key = entry.Mode + "|" + entry.Region;
                if (!groups.TryGetValue(key, out List<QueueEntry> group))
                {
                    group = new List<QueueEntry>();
                    groups.Add(key, group);
                }
                group.Add(entry);
            }

            foreach (List<QueueEntry> group in groups.Values)
            {
                TryFormMatchInGroup(group);
            }
        }

        private void TryFormMatchInGroup(List<QueueEntry> group)
        {
            // Parties are the matching units: group entries by PartyId (solo
            // players are their own one-person party) and sort by average rating.
            var parties = new List<List<QueueEntry>>();
            var partyIndex = new Dictionary<string, List<QueueEntry>>();
            foreach (QueueEntry entry in group)
            {
                string key = entry.Player.IsInParty ? entry.Player.PartyId : "solo:" + entry.Player.PlayerId;
                if (!partyIndex.TryGetValue(key, out List<QueueEntry> party))
                {
                    party = new List<QueueEntry>();
                    partyIndex.Add(key, party);
                    parties.Add(party);
                }
                party.Add(entry);
            }

            parties.Sort((a, b) => AverageRating(a).CompareTo(AverageRating(b)));

            // Greedily take whole parties in rating order until the lobby is full.
            var picked = new List<QueueEntry>();
            float oldestWait = 0f;
            foreach (List<QueueEntry> party in parties)
            {
                if (picked.Count + party.Count > _config.MaxPlayersPerMatch) continue;
                picked.AddRange(party);
                foreach (QueueEntry entry in party)
                    oldestWait = Mathf.Max(oldestWait, entry.WaitedSeconds);
                if (picked.Count >= _config.MaxPlayersPerMatch) break;
            }

            if (picked.Count < _config.MinPlayersPerMatch) return;

            float tolerance = _config.ToleranceForWait(oldestWait);
            if (RatingSpread(picked) > tolerance) return;

            FormLobby(picked, group[0].Mode, group[0].Region);
        }

        private void FormLobby(List<QueueEntry> picked, string mode, string region)
        {
            foreach (QueueEntry entry in picked)
                _queue.Remove(entry);

            var lobby = new Lobby(mode, region, _config.MaxPlayersPerMatch, picked[0].Player);
            for (int i = 1; i < picked.Count; i++)
                lobby.TryAddPlayer(picked[i].Player);
            _lobbies.Add(lobby.Id, lobby);

            if (_config.EnableReadyCheck)
            {
                lobby.SetState(LobbyState.ReadyCheck);
                var check = new ReadyCheck { Lobby = lobby, RemainingSeconds = _config.ReadyCheckTimeoutSeconds };
                foreach (PlayerInfo player in lobby.Players)
                    _readyChecksByPlayer.Add(player.PlayerId, check);
                OnReadyCheckStarted?.Invoke(lobby);
            }
            else
            {
                lobby.SetState(LobbyState.InMatch);
                OnMatchFound?.Invoke(lobby);
            }
            OnLobbyUpdated?.Invoke(lobby);
        }

        private void CompleteReadyCheck(ReadyCheck check)
        {
            foreach (PlayerInfo player in check.Lobby.Players)
                _readyChecksByPlayer.Remove(player.PlayerId);

            check.Lobby.SetState(LobbyState.InMatch);
            OnReadyCheckCompleted?.Invoke(check.Lobby);
            OnMatchFound?.Invoke(check.Lobby);
            OnLobbyUpdated?.Invoke(check.Lobby);
        }

        private void FailReadyCheck(ReadyCheck check, PlayerInfo cause)
        {
            // Remember who accepted: they go back in the queue, the decliner does not.
            var requeue = new List<QueueEntry>();
            foreach (string playerId in check.Accepted)
            {
                PlayerInfo player = FindPlayerInLobby(check.Lobby, playerId);
                if (player != null)
                    requeue.Add(new QueueEntry { Player = player, Mode = check.Lobby.Mode, Region = check.Lobby.Region, WaitedSeconds = 0f });
            }

            foreach (PlayerInfo player in check.Lobby.Players)
                _readyChecksByPlayer.Remove(player.PlayerId);

            check.Lobby.Close();
            _lobbies.Remove(check.Lobby.Id);

            foreach (QueueEntry entry in requeue)
            {
                _queue.Add(entry);
                OnQueueJoined?.Invoke(entry.Player);
            }

            OnReadyCheckFailed?.Invoke(check.Lobby, cause);
            OnLobbyUpdated?.Invoke(check.Lobby);
        }

        // ---------------------------------------------------------- helpers

        private QueueEntry FindQueueEntry(string playerId)
        {
            foreach (QueueEntry entry in _queue)
            {
                if (entry.Player.PlayerId == playerId) return entry;
            }
            return null;
        }

        private static PlayerInfo FindPlayerInLobby(Lobby lobby, string playerId)
        {
            if (lobby == null || string.IsNullOrEmpty(playerId)) return null;
            foreach (PlayerInfo player in lobby.Players)
            {
                if (player.PlayerId == playerId) return player;
            }
            return null;
        }

        private static PlayerInfo FirstNotAccepted(ReadyCheck check)
        {
            foreach (PlayerInfo player in check.Lobby.Players)
            {
                if (!check.Accepted.Contains(player.PlayerId)) return player;
            }
            return null;
        }

        private static float AverageRating(List<QueueEntry> party)
        {
            float sum = 0f;
            foreach (QueueEntry entry in party)
                sum += entry.Player.SkillRating;
            return party.Count == 0 ? 0f : sum / party.Count;
        }

        private static float RatingSpread(List<QueueEntry> entries)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (QueueEntry entry in entries)
            {
                min = Mathf.Min(min, entry.Player.SkillRating);
                max = Mathf.Max(max, entry.Player.SkillRating);
            }
            return max - min;
        }
    }
}
