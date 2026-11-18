using System;

namespace StarCollector.Scoring
{
    /// <summary>
    /// Computes the points of a collected star from the time taken to reach it
    /// and the accuracy of the collection.
    /// </summary>
    public sealed class ScoreCalculator
    {
        /// <summary>Points of a star collected within the target time with full accuracy.</summary>
        public const int MaxPointsPerStar = 1000;

        /// <summary>Up to this time the time factor is 1.</summary>
        public double TargetTimeSeconds { get; }

        /// <summary>From this time on the time factor is 0.</summary>
        public double MaxTimeSeconds { get; }

        public ScoreCalculator(double targetTimeSeconds = 2.0, double maxTimeSeconds = 10.0)
        {
            if (!IsFinite(targetTimeSeconds) || targetTimeSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTimeSeconds), "The target time must be a positive number.");
            }

            if (!IsFinite(maxTimeSeconds) || maxTimeSeconds <= targetTimeSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeSeconds), "The maximum time must be greater than the target time.");
            }

            TargetTimeSeconds = targetTimeSeconds;
            MaxTimeSeconds = maxTimeSeconds;
        }

        /// <summary>
        /// Points for one star: <see cref="MaxPointsPerStar"/> scaled by the time
        /// factor (1 up to the target time, then linear down to 0 at the maximum
        /// time) and by the accuracy (0 to 1).
        /// </summary>
        /// <param name="elapsedSeconds">Time taken to collect the star, zero or more.</param>
        /// <param name="accuracy">Accuracy of the collection, from 0 to 1.</param>
        public int Calculate(double elapsedSeconds, double accuracy)
        {
            if (!IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "The elapsed time must be zero or a positive number.");
            }

            if (!IsFinite(accuracy) || accuracy < 0 || accuracy > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(accuracy), "The accuracy must be between 0 and 1.");
            }

            return (int)Math.Round(MaxPointsPerStar * TimeFactor(elapsedSeconds) * accuracy, MidpointRounding.AwayFromZero);
        }

        private double TimeFactor(double elapsedSeconds)
        {
            if (elapsedSeconds <= TargetTimeSeconds)
            {
                return 1.0;
            }

            if (elapsedSeconds >= MaxTimeSeconds)
            {
                return 0.0;
            }

            return (MaxTimeSeconds - elapsedSeconds) / (MaxTimeSeconds - TargetTimeSeconds);
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
