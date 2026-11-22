using System;
using UnityEngine;

namespace Sdx17.Matchmaking
{
    /// <summary>
    /// One player as matchmaking sees them. Identity is the stable <see cref="PlayerId"/>
    /// (account id, not display name). <see cref="SkillRating"/> is a simple Elo-style
    /// number: higher means stronger. The matchmaker only ever compares ratings of
    /// players in the same mode and region, never across them.
    /// </summary>
    [Serializable]
    public class PlayerInfo
    {
        [SerializeField] private string playerId;
        [SerializeField] private string displayName;
        [SerializeField] private float skillRating = 1000f;
        [SerializeField] private string region = "eu-west";
        [SerializeField] private string partyId;

        public string PlayerId => playerId;
        public string DisplayName => displayName;
        public float SkillRating => skillRating;
        public string Region => region;

        /// <summary>
        /// Empty means the player queued solo. Players sharing a PartyId are always
        /// matched together or not at all; a party is never split across lobbies.
        /// </summary>
        public string PartyId => partyId;

        public bool IsInParty => !string.IsNullOrEmpty(partyId);

        public PlayerInfo(string playerId, string displayName, float skillRating, string region, string partyId = null)
        {
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("PlayerId must not be empty.", nameof(playerId));

            this.playerId = playerId;
            this.displayName = string.IsNullOrEmpty(displayName) ? playerId : displayName;
            this.skillRating = skillRating;
            this.region = string.IsNullOrEmpty(region) ? "eu-west" : region;
            this.partyId = partyId;
        }

        /// <summary>Adjusts the rating after a finished match. Positive delta = win.</summary>
        public void ApplyRatingDelta(float delta)
        {
            skillRating = Mathf.Max(0f, skillRating + delta);
        }
    }
}
