using System;
using UnityEngine;
using UnityEngine.Events;

namespace Sdx17.Matchmaking
{
    /// <summary>
    /// The single entry point the game talks to. Put this on a GameObject that lives
    /// for the whole session (for example next to your network / multiplayer
    /// manager), assign a <see cref="MatchmakingConfig"/>, and subscribe to the
    /// UnityEvents in the Inspector or the C# events in code.
    ///
    /// The service owns the backend: by default it creates a
    /// <see cref="MockMatchmakingBackend"/> from the config. Swap in a real server
    /// backend with <see cref="SetBackend"/> — game code never needs to change.
    ///
    /// Integration notes:
    /// - Multiplayer Architecture: when <see cref="OnMatchFound"/> fires, hand the
    ///   lobby's player list to your session / transport layer to start the game.
    ///   Matchmaking decides WHO plays together; the multiplayer layer decides
    ///   HOW they connect.
    /// - Combat: nothing here touches combat. Once the match starts, your combat
    ///   systems read the lobby roster (or your own player list) as usual.
    /// </summary>
    public class MatchmakingService : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private MatchmakingConfig config;

        [Header("Events (Inspector)")]
        [SerializeField] private UnityEvent<Lobby> onMatchFound = new UnityEvent<Lobby>();
        [SerializeField] private UnityEvent<PlayerInfo> onQueueTimeout = new UnityEvent<PlayerInfo>();
        [SerializeField] private UnityEvent<Lobby> onReadyCheckStarted = new UnityEvent<Lobby>();
        [SerializeField] private UnityEvent<Lobby> onReadyCheckCompleted = new UnityEvent<Lobby>();

        /// <summary>C# event mirror of <see cref="onMatchFound"/> for code subscribers.</summary>
        public event Action<Lobby> MatchFound;
        public event Action<PlayerInfo> QueueTimedOut;
        public event Action<Lobby> ReadyCheckStarted;
        public event Action<Lobby> ReadyCheckCompleted;
        public event Action<Lobby, PlayerInfo> ReadyCheckFailed;

        public MatchmakingConfig Config => config;
        public IMatchmakingBackend Backend { get; private set; }
        public bool IsInitialized => Backend != null;

        /// <summary>True while the local player (see <see cref="LocalPlayer"/>) is waiting in the queue.</summary>
        public bool IsInQueue { get; private set; }

        /// <summary>The lobby the local player is currently matched into, if any.</summary>
        public Lobby CurrentLobby { get; private set; }

        /// <summary>
        /// Set this before calling <see cref="JoinQueue"/> so the service knows
        /// which player "you" are for ready-checks and queue state.
        /// </summary>
        public PlayerInfo LocalPlayer { get; set; }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (Backend is ITickableBackend tickable)
                tickable.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            UnsubscribeFromBackend();
        }

        /// <summary>Creates the default mock backend from the config. Called from Awake; safe to call again after <see cref="SetBackend"/>.</summary>
        public void Initialize()
        {
            if (Backend != null) return;
            if (config == null)
            {
                Debug.LogWarning("[Sdx17 Matchmaking] No MatchmakingConfig assigned; using built-in defaults. Create one via Sdx17 > Matchmaking > Create Matchmaking Config.");
                config = ScriptableObject.CreateInstance<MatchmakingConfig>();
            }
            SetBackend(new MockMatchmakingBackend(config));
        }

        /// <summary>Replace the backend (for example with a real server backend). The old backend is unsubscribed first.</summary>
        public void SetBackend(IMatchmakingBackend backend)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            UnsubscribeFromBackend();
            Backend = backend;
            SubscribeToBackend();
        }

        // ---------------------------------------------------------- queue API

        /// <summary>Queue the local player. Returns false when no local player is set, the mode/region is invalid, or the player is already queued.</summary>
        public bool JoinQueue(string mode = null, string region = null)
        {
            if (!IsInitialized) return false;
            if (LocalPlayer == null)
            {
                Debug.LogWarning("[Sdx17 Matchmaking] JoinQueue called with no LocalPlayer set.");
                return false;
            }
            return JoinQueue(LocalPlayer, mode, region);
        }

        /// <summary>Queue any player (used by tests, bots, and the demo). The local-player tracking only updates when the player is <see cref="LocalPlayer"/>.</summary>
        public bool JoinQueue(PlayerInfo player, string mode = null, string region = null)
        {
            if (!IsInitialized || player == null) return false;
            string useMode = string.IsNullOrEmpty(mode) ? config.DefaultMode : mode;
            string useRegion = string.IsNullOrEmpty(region) ? config.DefaultRegion : region;
            bool joined = Backend.JoinQueue(player, useMode, useRegion);
            if (joined && player == LocalPlayer)
                IsInQueue = true;
            return joined;
        }

        /// <summary>Leave the queue. Returns false when the local player is not queued.</summary>
        public bool LeaveQueue()
        {
            if (!IsInitialized || LocalPlayer == null) return false;
            bool left = Backend.LeaveQueue(LocalPlayer.PlayerId);
            if (left) IsInQueue = false;
            return left;
        }

        /// <summary>Cancel queueing (same effect as <see cref="LeaveQueue"/>).</summary>
        public bool CancelQueue()
        {
            if (!IsInitialized || LocalPlayer == null) return false;
            bool cancelled = Backend.CancelQueue(LocalPlayer.PlayerId);
            if (cancelled) IsInQueue = false;
            return cancelled;
        }

        // ---------------------------------------------------------- ready-check API

        public bool AcceptReadyCheck()
        {
            if (!IsInitialized || LocalPlayer == null) return false;
            return Backend.AcceptReadyCheck(LocalPlayer.PlayerId);
        }

        public bool DeclineReadyCheck()
        {
            if (!IsInitialized || LocalPlayer == null) return false;
            return Backend.DeclineReadyCheck(LocalPlayer.PlayerId);
        }

        // ---------------------------------------------------------- lobby API

        public Lobby CreateLobby(string mode = null, string region = null, int maxPlayers = 0)
        {
            if (!IsInitialized || LocalPlayer == null) return null;
            string useMode = string.IsNullOrEmpty(mode) ? config.DefaultMode : mode;
            string useRegion = string.IsNullOrEmpty(region) ? config.DefaultRegion : region;
            int useMax = maxPlayers > 0 ? maxPlayers : config.MaxPlayersPerMatch;
            Lobby lobby = Backend.CreateLobby(LocalPlayer, useMode, useRegion, useMax);
            if (lobby != null) CurrentLobby = lobby;
            return lobby;
        }

        public bool JoinLobby(string lobbyId)
        {
            if (!IsInitialized || LocalPlayer == null) return false;
            bool joined = Backend.JoinLobby(lobbyId, LocalPlayer);
            if (joined) CurrentLobby = Backend.GetLobby(lobbyId);
            return joined;
        }

        public bool LeaveLobby()
        {
            if (!IsInitialized || LocalPlayer == null || CurrentLobby == null) return false;
            bool left = Backend.LeaveLobby(CurrentLobby.Id, LocalPlayer.PlayerId);
            if (left) CurrentLobby = null;
            return left;
        }

        // ---------------------------------------------------------- backend events

        private void SubscribeToBackend()
        {
            Backend.OnMatchFound += HandleMatchFound;
            Backend.OnQueueTimeout += HandleQueueTimeout;
            Backend.OnReadyCheckStarted += HandleReadyCheckStarted;
            Backend.OnReadyCheckCompleted += HandleReadyCheckCompleted;
            Backend.OnReadyCheckFailed += HandleReadyCheckFailed;
        }

        private void UnsubscribeFromBackend()
        {
            if (Backend == null) return;
            Backend.OnMatchFound -= HandleMatchFound;
            Backend.OnQueueTimeout -= HandleQueueTimeout;
            Backend.OnReadyCheckStarted -= HandleReadyCheckStarted;
            Backend.OnReadyCheckCompleted -= HandleReadyCheckCompleted;
            Backend.OnReadyCheckFailed -= HandleReadyCheckFailed;
        }

        private void HandleMatchFound(Lobby lobby)
        {
            IsInQueue = false;
            if (LocalPlayer != null && lobby.ContainsPlayer(LocalPlayer.PlayerId))
                CurrentLobby = lobby;
            onMatchFound?.Invoke(lobby);
            MatchFound?.Invoke(lobby);
        }

        private void HandleQueueTimeout(PlayerInfo player)
        {
            if (player == LocalPlayer) IsInQueue = false;
            onQueueTimeout?.Invoke(player);
            QueueTimedOut?.Invoke(player);
        }

        private void HandleReadyCheckStarted(Lobby lobby)
        {
            if (LocalPlayer != null && lobby.ContainsPlayer(LocalPlayer.PlayerId))
                CurrentLobby = lobby;
            onReadyCheckStarted?.Invoke(lobby);
            ReadyCheckStarted?.Invoke(lobby);
        }

        private void HandleReadyCheckCompleted(Lobby lobby)
        {
            onReadyCheckCompleted?.Invoke(lobby);
            ReadyCheckCompleted?.Invoke(lobby);
        }

        private void HandleReadyCheckFailed(Lobby lobby, PlayerInfo cause)
        {
            if (CurrentLobby == lobby) CurrentLobby = null;
            ReadyCheckFailed?.Invoke(lobby, cause);
        }
    }
}
