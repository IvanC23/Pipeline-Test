using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StarCollector.Online
{
    /// <summary>
    /// Access to the online leaderboard. The game depends only on this
    /// interface, so tests can use a fake implementation without a network.
    /// </summary>
    public interface ILeaderboardClient
    {
        /// <summary>
        /// Sends the score of a match. The stored entry must have the same
        /// player, match and score as the entry sent.
        /// </summary>
        Task<SubmissionResult> SubmitScoreAsync(ScoreEntry entry, CancellationToken cancellationToken = default);

        /// <summary>Best scores, highest first; at most <paramref name="count"/> entries.</summary>
        Task<IReadOnlyList<ScoreEntry>> GetTopScoresAsync(int count, CancellationToken cancellationToken = default);
    }
}
