using System;

namespace Sdx17.Matchmaking
{
    /// <summary>
    /// Backends that need per-frame work (timeouts, ready-check deadlines, match
    /// attempts) implement this. <see cref="MatchmakingService"/> calls Tick from
    /// its Update; a server backend can ignore it or drive it from its own loop.
    /// </summary>
    public interface ITickableBackend
    {
        void Tick(float deltaTime);
    }

    /// <summary>
    /// The contract every matchmaking backend must fulfil. The game only ever talks
    /// to this interface (via <see cref="MatchmakingService"/>), so the local mock
    /// backend can be swapped for a real server backend without touching game code.
    ///
    /// Events are the source of truth: a call returning true only means the request
    /// was accepted, the event tells you what actually happened.
    /// </summary>
    public interface IMatchmakingBackend
    {
        /// <summary>A match is confirmed and ready to play (ready-check passed, or ready-check disabled).</summary>
        event Action<Lobby> OnMatchFound;

        /// <summary>A player waited longer than the queue timeout and was removed from the queue.</summary>
        event Action<PlayerInfo> OnQueueTimeout;

        /// <summary>A player entered the queue.</summary>
        event Action<PlayerInfo> OnQueueJoined;

        /// <summary>A player left the queue (by request, not by timeout).</summary>
        event Action<PlayerInfo> OnQueueLeft;

        /// <summary>A match was proposed; every player in the lobby must now accept or decline.</summary>
        event Action<Lobby> OnReadyCheckStarted;

        /// <summary>Every player accepted the ready-check; the match is confirmed (OnMatchFound follows).</summary>
        event Action<Lobby> OnReadyCheckCompleted;

        /// <summary>A player declined or the ready-check timed out; the lobby was dissolved.</summary>
        event Action<Lobby, PlayerInfo> OnReadyCheckFailed;

        /// <summary>A lobby's membership or state changed.</summary>
        event Action<Lobby> OnLobbyUpdated;

        /// <summary>Join the queue for a mode/region. Returns false if the player is already queued, the mode/region is not allowed, or the request is invalid.</summary>
        bool JoinQueue(PlayerInfo player, string mode, string region);

        /// <summary>Leave the queue voluntarily. Returns false if the player was not queued.</summary>
        bool LeaveQueue(string playerId);

        /// <summary>Cancel queueing. Same effect as <see cref="LeaveQueue"/>; kept as a named operation because UI code reads better with it.</summary>
        bool CancelQueue(string playerId);

        /// <summary>The player accepts the ready-check for the lobby they were matched into.</summary>
        bool AcceptReadyCheck(string playerId);

        /// <summary>The player declines the ready-check; the lobby is dissolved.</summary>
        bool DeclineReadyCheck(string playerId);

        /// <summary>Create a lobby directly (custom games, party leader flow) without going through the queue.</summary>
        Lobby CreateLobby(PlayerInfo host, string mode, string region, int maxPlayers);

        /// <summary>Join an existing lobby by id. Returns false if it is full, closed, or in a match.</summary>
        bool JoinLobby(string lobbyId, PlayerInfo player);

        /// <summary>Leave a lobby by id. Returns false if the player was not in it.</summary>
        bool LeaveLobby(string lobbyId, string playerId);

        /// <summary>Look up a lobby by id, or null when it does not exist.</summary>
        Lobby GetLobby(string lobbyId);

        /// <summary>How many players are currently waiting in the queue (any mode/region).</summary>
        int QueuedPlayerCount { get; }
    }
}
