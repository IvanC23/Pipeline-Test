using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StarCollector.Online
{
    /// <summary>
    /// In-memory leaderboard for development and tests: no network, no
    /// persistence. Accepts one score per player and match; a second
    /// submission for the same player and match is rejected.
    /// </summary>
    public sealed class FakeLeaderboardClient : ILeaderboardClient
    {
        private readonly List<ScoreEntry> entries = new List<ScoreEntry>();
        private readonly object entriesLock = new object();

        public Task<SubmissionResult> SubmitScoreAsync(ScoreEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock (entriesLock)
            {
                var duplicate = entries.Any(stored =>
                    string.Equals(stored.PlayerId, entry.PlayerId, StringComparison.Ordinal)
                    && string.Equals(stored.MatchId, entry.MatchId, StringComparison.Ordinal));

                if (duplicate)
                {
                    return Task.FromResult(SubmissionResult.Rejected($"A score for player '{entry.PlayerId}' in match '{entry.MatchId}' was already submitted."));
                }

                // Stored as a new instance, like a server would return it.
                var stored = new ScoreEntry(entry.PlayerId, entry.MatchId, entry.Score);
                entries.Add(stored);
                return Task.FromResult(SubmissionResult.Success(stored));
            }
        }

        public Task<IReadOnlyList<ScoreEntry>> GetTopScoresAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "The count cannot be negative.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock (entriesLock)
            {
                // OrderByDescending is stable: equal scores keep the submission order.
                IReadOnlyList<ScoreEntry> top = entries
                    .OrderByDescending(entry => entry.Score)
                    .Take(count)
                    .ToList();
                return Task.FromResult(top);
            }
        }
    }
}
