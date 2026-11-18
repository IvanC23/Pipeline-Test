using System;

namespace StarCollector.Online
{
    /// <summary>
    /// Score of one player in one match, as sent to and read from the leaderboard.
    /// </summary>
    public sealed class ScoreEntry : IEquatable<ScoreEntry>
    {
        public string PlayerId { get; }
        public string MatchId { get; }
        public int Score { get; }

        public ScoreEntry(string playerId, string matchId, int score)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("The player id is empty.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("The match id is empty.", nameof(matchId));
            }

            if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), "The score cannot be negative.");
            }

            PlayerId = playerId;
            MatchId = matchId;
            Score = score;
        }

        public bool Equals(ScoreEntry other) =>
            other != null
            && string.Equals(PlayerId, other.PlayerId, StringComparison.Ordinal)
            && string.Equals(MatchId, other.MatchId, StringComparison.Ordinal)
            && Score == other.Score;

        public override bool Equals(object obj) => Equals(obj as ScoreEntry);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(PlayerId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(MatchId);
                return (hash * 397) ^ Score;
            }
        }

        public override string ToString() => $"{PlayerId} / {MatchId}: {Score}";
    }
}
