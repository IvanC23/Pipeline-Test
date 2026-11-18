using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using StarCollector.Online;

namespace StarCollector.Tests.EditMode.Online
{
    /// <summary>
    /// Tests on the leaderboard client through its interface, with the
    /// in-memory implementation: no network is used. The fake completes its
    /// tasks synchronously, so the results are read without awaiting.
    /// </summary>
    public class FakeLeaderboardClientTests
    {
        private ILeaderboardClient client;

        [SetUp]
        public void CreateClient()
        {
            client = new FakeLeaderboardClient();
        }

        [Test]
        [Category("DATA-01")]
        public void Submit_StoredEntryHasTheSamePlayerMatchAndScore()
        {
            var sent = new ScoreEntry("player-001", "match-001", 1234);

            var result = Submit(sent);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.StoredEntry.PlayerId, Is.EqualTo(sent.PlayerId));
            Assert.That(result.StoredEntry.MatchId, Is.EqualTo(sent.MatchId));
            Assert.That(result.StoredEntry.Score, Is.EqualTo(sent.Score));
        }

        [Test]
        [Category("DATA-01")]
        public void Submit_ReadBackEntryEqualsTheEntrySent()
        {
            var sent = new ScoreEntry("player-001", "match-001", 1234);
            Submit(sent);

            var top = TopScores(10);

            Assert.That(top, Is.EqualTo(new[] { sent }));
        }

        [Test]
        [Category("DATA-01")]
        public void ScoreEntry_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(() => new ScoreEntry(" ", "match-001", 10));
            Assert.Throws<ArgumentException>(() => new ScoreEntry("player-001", "", 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreEntry("player-001", "match-001", -1));
        }

        [Test]
        [Category("DATA-02")]
        public void Submit_SamePlayerAndMatchTwice_SecondIsRejected()
        {
            Submit(new ScoreEntry("player-001", "match-001", 100));

            var second = Submit(new ScoreEntry("player-001", "match-001", 900));

            Assert.That(second.Accepted, Is.False);
            Assert.That(second.StoredEntry, Is.Null);
            Assert.That(second.Error, Is.Not.Empty);
            Assert.That(TopScores(10).Single().Score, Is.EqualTo(100), "The first score must be kept.");
        }

        [Test]
        [Category("DATA-02")]
        public void Submit_SamePlayerInAnotherMatch_IsAccepted()
        {
            Submit(new ScoreEntry("player-001", "match-001", 100));

            var other = Submit(new ScoreEntry("player-001", "match-002", 200));

            Assert.That(other.Accepted, Is.True);
            Assert.That(TopScores(10).Count, Is.EqualTo(2));
        }

        [Test]
        [Category("NET-01")]
        public void GetTopScores_ReturnsHighestFirstAndAtMostCount()
        {
            Submit(new ScoreEntry("player-001", "match-001", 300));
            Submit(new ScoreEntry("player-002", "match-001", 900));
            Submit(new ScoreEntry("player-003", "match-001", 600));

            var top = TopScores(2);

            Assert.That(top.Select(entry => entry.Score), Is.EqualTo(new[] { 900, 600 }));
        }

        [Test]
        [Category("NET-01")]
        public void GetTopScores_EqualScores_KeepTheSubmissionOrder()
        {
            Submit(new ScoreEntry("player-001", "match-001", 500));
            Submit(new ScoreEntry("player-002", "match-001", 500));

            var top = TopScores(10);

            Assert.That(top.Select(entry => entry.PlayerId), Is.EqualTo(new[] { "player-001", "player-002" }));
        }

        [Test]
        [Category("NET-01")]
        public void GetTopScores_CountZeroOrEmptyLeaderboard_ReturnsEmpty()
        {
            Assert.That(TopScores(10), Is.Empty);

            Submit(new ScoreEntry("player-001", "match-001", 500));

            Assert.That(TopScores(0), Is.Empty);
        }

        [Test]
        [Category("NET-01")]
        public void GetTopScores_NegativeCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => client.GetTopScoresAsync(-1));
        }

        [Test]
        [Category("NET-01")]
        public void CancelledRequests_ThrowAndStoreNothing()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Assert.Throws<OperationCanceledException>(() =>
                    client.SubmitScoreAsync(new ScoreEntry("player-001", "match-001", 100), cancellation.Token));
                Assert.Throws<OperationCanceledException>(() => client.GetTopScoresAsync(10, cancellation.Token));
            }

            Assert.That(TopScores(10), Is.Empty);
        }

        private SubmissionResult Submit(ScoreEntry entry) =>
            client.SubmitScoreAsync(entry).GetAwaiter().GetResult();

        private System.Collections.Generic.IReadOnlyList<ScoreEntry> TopScores(int count) =>
            client.GetTopScoresAsync(count).GetAwaiter().GetResult();
    }
}
