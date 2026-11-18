using System;

namespace StarCollector.Scoring
{
    /// <summary>
    /// Adapts the difficulty to the player's results. After a series of missed
    /// stars the next stars spawn closer and need less accuracy; after a series
    /// of collected stars both values move back towards the base values.
    /// Values never go below the minimums or above the base values.
    /// </summary>
    public sealed class DifficultyAdapter
    {
        public const double BaseTargetDistance = 10.0;
        public const double MinTargetDistance = 4.0;
        public const double TargetDistanceStep = 2.0;

        public const double BaseAccuracyThreshold = 0.8;
        public const double MinAccuracyThreshold = 0.5;
        public const double AccuracyThresholdStep = 0.1;

        /// <summary>Consecutive missed stars that make the game easier by one step.</summary>
        public const int MissesBeforeEasing = 3;

        /// <summary>Consecutive collected stars that make the game harder by one step.</summary>
        public const int HitsBeforeHardening = 5;

        private int consecutiveMisses;
        private int consecutiveHits;

        /// <summary>Distance from the player at which the next star spawns.</summary>
        public double TargetDistance { get; private set; } = BaseTargetDistance;

        /// <summary>Minimum accuracy needed to collect the next star.</summary>
        public double AccuracyThreshold { get; private set; } = BaseAccuracyThreshold;

        public void RecordHit()
        {
            consecutiveMisses = 0;
            consecutiveHits++;

            if (consecutiveHits < HitsBeforeHardening)
            {
                return;
            }

            consecutiveHits = 0;
            TargetDistance = Math.Min(BaseTargetDistance, TargetDistance + TargetDistanceStep);
            AccuracyThreshold = Math.Min(BaseAccuracyThreshold, Round(AccuracyThreshold + AccuracyThresholdStep));
        }

        public void RecordMiss()
        {
            consecutiveHits = 0;
            consecutiveMisses++;

            if (consecutiveMisses < MissesBeforeEasing)
            {
                return;
            }

            consecutiveMisses = 0;
            TargetDistance = Math.Max(MinTargetDistance, TargetDistance - TargetDistanceStep);
            AccuracyThreshold = Math.Max(MinAccuracyThreshold, Round(AccuracyThreshold - AccuracyThresholdStep));
        }

        /// <summary>Back to the base values, for a new match.</summary>
        public void Reset()
        {
            consecutiveMisses = 0;
            consecutiveHits = 0;
            TargetDistance = BaseTargetDistance;
            AccuracyThreshold = BaseAccuracyThreshold;
        }

        // Keeps thresholds such as 0.7 exact after repeated additions of 0.1.
        private static double Round(double value) => Math.Round(value, 2);
    }
}
