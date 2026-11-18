namespace StarCollector.Online
{
    /// <summary>
    /// Answer of the leaderboard to a score submission.
    /// </summary>
    public sealed class SubmissionResult
    {
        public bool Accepted { get; }

        /// <summary>The entry as stored by the leaderboard; null when rejected.</summary>
        public ScoreEntry StoredEntry { get; }

        /// <summary>Reason of the rejection; null when accepted.</summary>
        public string Error { get; }

        private SubmissionResult(bool accepted, ScoreEntry storedEntry, string error)
        {
            Accepted = accepted;
            StoredEntry = storedEntry;
            Error = error;
        }

        public static SubmissionResult Success(ScoreEntry storedEntry) => new SubmissionResult(true, storedEntry, null);

        public static SubmissionResult Rejected(string error) => new SubmissionResult(false, null, error);
    }
}
