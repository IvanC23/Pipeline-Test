using NUnit.Framework;
using StarCollector.Scoring;

namespace StarCollector.Tests.EditMode.Scoring
{
    public class DifficultyAdapterTests
    {
        private DifficultyAdapter adapter;

        [SetUp]
        public void CreateAdapter()
        {
            adapter = new DifficultyAdapter();
        }

        [Test]
        [Property("Requirement", "FUNC-02")]
        public void NewAdapter_StartsAtBaseValues()
        {
            AssertValues(DifficultyAdapter.BaseTargetDistance, DifficultyAdapter.BaseAccuracyThreshold);
        }

        [Test]
        [Property("Requirement", "FUNC-02")]
        public void ConsecutiveMisses_BelowLimit_DoNotChangeDifficulty()
        {
            Repeat(adapter.RecordMiss, DifficultyAdapter.MissesBeforeEasing - 1);

            AssertValues(DifficultyAdapter.BaseTargetDistance, DifficultyAdapter.BaseAccuracyThreshold);
        }

        [Test]
        [Property("Requirement", "FUNC-02")]
        public void ConsecutiveMisses_AtLimit_EaseByOneStep()
        {
            Repeat(adapter.RecordMiss, DifficultyAdapter.MissesBeforeEasing);

            AssertValues(8.0, 0.7);
        }

        [Test]
        [Property("Requirement", "FUNC-02")]
        public void Hit_ResetsTheMissCount()
        {
            adapter.RecordMiss();
            adapter.RecordMiss();
            adapter.RecordHit();
            adapter.RecordMiss();
            adapter.RecordMiss();

            AssertValues(DifficultyAdapter.BaseTargetDistance, DifficultyAdapter.BaseAccuracyThreshold);
        }

        [Test]
        [Property("Requirement", "FUNC-02")]
        public void ConsecutiveHits_AfterEasing_HardenByOneStep()
        {
            Repeat(adapter.RecordMiss, DifficultyAdapter.MissesBeforeEasing);
            Repeat(adapter.RecordHit, DifficultyAdapter.HitsBeforeHardening - 1);
            AssertValues(8.0, 0.7);

            adapter.RecordHit();

            AssertValues(DifficultyAdapter.BaseTargetDistance, DifficultyAdapter.BaseAccuracyThreshold);
        }

        [Test]
        [Property("Requirement", "FUNC-02")]
        public void Reset_RestoresBaseValues()
        {
            Repeat(adapter.RecordMiss, DifficultyAdapter.MissesBeforeEasing * 2);

            adapter.Reset();

            AssertValues(DifficultyAdapter.BaseTargetDistance, DifficultyAdapter.BaseAccuracyThreshold);
        }

        [Test]
        [Property("Requirement", "RISK-01")]
        public void ManyMisses_NeverGoBelowMinimums()
        {
            Repeat(adapter.RecordMiss, DifficultyAdapter.MissesBeforeEasing * 20);

            AssertValues(DifficultyAdapter.MinTargetDistance, DifficultyAdapter.MinAccuracyThreshold);
        }

        [Test]
        [Property("Requirement", "RISK-01")]
        public void ManyHits_NeverGoAboveBaseValues()
        {
            Repeat(adapter.RecordHit, DifficultyAdapter.HitsBeforeHardening * 20);

            AssertValues(DifficultyAdapter.BaseTargetDistance, DifficultyAdapter.BaseAccuracyThreshold);
        }

        [Test]
        [Property("Requirement", "RISK-01")]
        public void AlternatingSeries_StayWithinLimits()
        {
            for (var round = 0; round < 10; round++)
            {
                Repeat(adapter.RecordMiss, DifficultyAdapter.MissesBeforeEasing * 3);
                Assert.That(adapter.TargetDistance, Is.GreaterThanOrEqualTo(DifficultyAdapter.MinTargetDistance));
                Assert.That(adapter.AccuracyThreshold, Is.GreaterThanOrEqualTo(DifficultyAdapter.MinAccuracyThreshold));

                Repeat(adapter.RecordHit, DifficultyAdapter.HitsBeforeHardening * 3);
                Assert.That(adapter.TargetDistance, Is.LessThanOrEqualTo(DifficultyAdapter.BaseTargetDistance));
                Assert.That(adapter.AccuracyThreshold, Is.LessThanOrEqualTo(DifficultyAdapter.BaseAccuracyThreshold));
            }
        }

        private static void Repeat(System.Action action, int times)
        {
            for (var index = 0; index < times; index++)
            {
                action();
            }
        }

        // The adapter rounds the thresholds, so exact comparisons are safe.
        private void AssertValues(double targetDistance, double accuracyThreshold)
        {
            Assert.That(adapter.TargetDistance, Is.EqualTo(targetDistance), "Target distance");
            Assert.That(adapter.AccuracyThreshold, Is.EqualTo(accuracyThreshold), "Accuracy threshold");
        }
    }
}
