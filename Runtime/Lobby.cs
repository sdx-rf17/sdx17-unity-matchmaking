using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sdx17.Matchmaking
{
    public enum LobbyState
    {
        /// <summary>Forming: players are being added, match not started.</summary>
        Waiting,
        /// <summary>Match proposed: every player must accept before play starts.</summary>
        ReadyCheck,
        /// <summary>All players accepted; the match is live.</summary>
        InMatch,
        /// <summary>Closed for good: finished, dissolved, or abandoned. Never reopens.</summary>
        Closed
    }

    /// <summary>
    /// A group of players matched together for one game. A lobby is created by the
    /// backend (from the queue) or directly via <see cref="IMatchmakingBackend.CreateLobby"/>.
    /// Server-authoritative mindset: the backend owns lobby membership and state;
    /// clients only request changes and react to events.
    /// </summary>
    [Serializable]
    public class Lobby
    {
        [SerializeField] private string id;
        [SerializeField] private string mode;
        [SerializeField] private string region;
        [SerializeField] private int maxPlayers;
        [SerializeField] private LobbyState state;
        [SerializeField] private string hostPlayerId;
        [SerializeField] private List<PlayerInfo> players = new List<PlayerInfo>();

        public string Id => id;
        public string Mode => mode;
        public string Region => region;
        public int MaxPlayers => maxPlayers;
        public LobbyState State => state;
        public string HostPlayerId => hostPlayerId;
        public IReadOnlyList<PlayerInfo> Players => players;
        public int PlayerCount => players.Count;
        public bool IsFull => players.Count >= maxPlayers;
        public bool IsEmpty => players.Count == 0;
        public DateTime CreatedAtUtc { get; private set; }

        public Lobby(string mode, string region, int maxPlayers, PlayerInfo host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (maxPlayers < 2) throw new ArgumentOutOfRangeException(nameof(maxPlayers), "A lobby needs at least 2 players.");
            if (string.IsNullOrEmpty(mode)) throw new ArgumentException("Mode must not be empty.", nameof(mode));

            id = Guid.NewGuid().ToString("N");
            this.mode = mode;
            this.region = string.IsNullOrEmpty(region) ? "eu-west" : region;
            this.maxPlayers = maxPlayers;
            state = LobbyState.Waiting;
            hostPlayerId = host.PlayerId;
            CreatedAtUtc = DateTime.UtcNow;
            players.Add(host);
        }

        /// <summary>Internal constructor used by backends when rebuilding a lobby from data.</summary>
        internal Lobby(string id, string mode, string region, int maxPlayers, LobbyState state, string hostPlayerId, IEnumerable<PlayerInfo> players, DateTime createdAtUtc)
        {
            this.id = id;
            this.mode = mode;
            this.region = region;
            this.maxPlayers = maxPlayers;
            this.state = state;
            this.hostPlayerId = hostPlayerId;
            this.players = new List<PlayerInfo>(players ?? Array.Empty<PlayerInfo>());
            CreatedAtUtc = createdAtUtc;
        }

        public bool ContainsPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            foreach (PlayerInfo p in players)
            {
                if (p.PlayerId == playerId) return true;
            }
            return false;
        }

        public bool TryAddPlayer(PlayerInfo player)
        {
            if (player == null) return false;
            if (state == LobbyState.Closed || state == LobbyState.InMatch) return false;
            if (IsFull) return false;
            if (ContainsPlayer(player.PlayerId)) return false;

            players.Add(player);
            return true;
        }

        public bool RemovePlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId == playerId)
                {
                    players.RemoveAt(i);
                    if (hostPlayerId == playerId && players.Count > 0)
                        hostPlayerId = players[0].PlayerId;
                    if (players.Count == 0)
                        state = LobbyState.Closed;
                    return true;
                }
            }
            return false;
        }

        internal void SetState(LobbyState newState)
        {
            // Closed is terminal: nothing reopens a closed lobby.
            if (state == LobbyState.Closed && newState != LobbyState.Closed) return;
            state = newState;
        }

        internal void Close()
        {
            state = LobbyState.Closed;
        }
    }
}
