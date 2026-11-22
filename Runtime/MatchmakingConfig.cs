using UnityEngine;

namespace Sdx17.Matchmaking
{
    /// <summary>
    /// Tunables for matchmaking. Create one via
    /// Sdx17 &gt; Matchmaking &gt; Create Matchmaking Config, assign it to a
    /// <see cref="MatchmakingService"/>, and tune from the Inspector.
    /// Defaults are for small local tests (2v2-ish lobbies, short timeouts);
    /// raise them for real play.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchmakingConfig", menuName = "Sdx17/Matchmaking/Matchmaking Config")]
    public class MatchmakingConfig : ScriptableObject
    {
        [Header("Match size")]
        [SerializeField] private int maxPlayersPerMatch = 4;
        [SerializeField] private int minPlayersPerMatch = 2;

        [Header("Modes and regions")]
        [SerializeField] private string[] allowedModes = { "duel", "team", "ffa" };
        [SerializeField] private string[] allowedRegions = { "eu-west", "eu-east", "na-east" };
        [SerializeField] private string defaultMode = "duel";
        [SerializeField] private string defaultRegion = "eu-west";

        [Header("Timing (seconds)")]
        [SerializeField] private float queueTimeoutSeconds = 60f;
        [SerializeField] private float readyCheckTimeoutSeconds = 15f;

        [Header("Skill matching")]
        [Tooltip("Two players match when their ratings differ by no more than this at queue time.")]
        [SerializeField] private float skillTolerance = 100f;
        [Tooltip("Extra tolerance added per second the oldest waiting player has waited, so long queues still resolve instead of timing out.")]
        [SerializeField] private float skillToleranceGrowthPerSecond = 5f;
        [SerializeField] private float maxSkillTolerance = 400f;

        [Header("Flow")]
        [Tooltip("When true, a found match waits for every player to accept (ready-check). When false, the match starts immediately.")]
        [SerializeField] private bool enableReadyCheck = true;

        public int MaxPlayersPerMatch => maxPlayersPerMatch;
        public int MinPlayersPerMatch => minPlayersPerMatch;
        public string[] AllowedModes => allowedModes;
        public string[] AllowedRegions => allowedRegions;
        public string DefaultMode => defaultMode;
        public string DefaultRegion => defaultRegion;
        public float QueueTimeoutSeconds => queueTimeoutSeconds;
        public float ReadyCheckTimeoutSeconds => readyCheckTimeoutSeconds;
        public float SkillTolerance => skillTolerance;
        public float SkillToleranceGrowthPerSecond => skillToleranceGrowthPerSecond;
        public float MaxSkillTolerance => maxSkillTolerance;
        public bool EnableReadyCheck => enableReadyCheck;

        /// <summary>Tolerance in effect for a player who has waited this long.</summary>
        public float ToleranceForWait(float waitedSeconds)
        {
            float tolerance = skillTolerance + skillToleranceGrowthPerSecond * Mathf.Max(0f, waitedSeconds);
            return Mathf.Min(tolerance, maxSkillTolerance);
        }

        public bool IsModeAllowed(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return false;
            foreach (string m in allowedModes)
            {
                if (m == mode) return true;
            }
            return false;
        }

        public bool IsRegionAllowed(string region)
        {
            if (string.IsNullOrEmpty(region)) return false;
            foreach (string r in allowedRegions)
            {
                if (r == region) return true;
            }
            return false;
        }

        private void OnValidate()
        {
            maxPlayersPerMatch = Mathf.Clamp(maxPlayersPerMatch, 2, 64);
            minPlayersPerMatch = Mathf.Clamp(minPlayersPerMatch, 2, maxPlayersPerMatch);
            queueTimeoutSeconds = Mathf.Max(5f, queueTimeoutSeconds);
            readyCheckTimeoutSeconds = Mathf.Max(5f, readyCheckTimeoutSeconds);
            skillTolerance = Mathf.Max(0f, skillTolerance);
            skillToleranceGrowthPerSecond = Mathf.Max(0f, skillToleranceGrowthPerSecond);
            maxSkillTolerance = Mathf.Max(skillTolerance, maxSkillTolerance);

            if (allowedModes == null || allowedModes.Length == 0)
                allowedModes = new[] { "duel" };
            if (allowedRegions == null || allowedRegions.Length == 0)
                allowedRegions = new[] { "eu-west" };
            if (!IsModeAllowed(defaultMode))
                defaultMode = allowedModes[0];
            if (!IsRegionAllowed(defaultRegion))
                defaultRegion = allowedRegions[0];
        }
    }
}
