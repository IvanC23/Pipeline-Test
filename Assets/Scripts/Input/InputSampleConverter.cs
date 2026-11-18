using System;
using System.Collections.Generic;

namespace StarCollector.InputConversion
{
    /// <summary>
    /// Converts a sequence of input samples into movement parameters: average
    /// velocity between the first and the last sample, as unit direction and
    /// speed scaled to <see cref="MaxSpeed"/>.
    /// </summary>
    public sealed class InputSampleConverter
    {
        /// <summary>Speed (units per second) that maps to a normalized speed of 1.</summary>
        public double MaxSpeed { get; }

        /// <summary>Below this speed the movement is treated as noise and not valid.</summary>
        public double MinSpeed { get; }

        public InputSampleConverter(double maxSpeed = 20.0, double minSpeed = 0.5)
        {
            if (double.IsNaN(maxSpeed) || double.IsInfinity(maxSpeed) || maxSpeed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeed), "The maximum speed must be a positive number.");
            }

            if (double.IsNaN(minSpeed) || minSpeed < 0 || minSpeed >= maxSpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(minSpeed), "The minimum speed must be zero or more and less than the maximum speed.");
            }

            MaxSpeed = maxSpeed;
            MinSpeed = minSpeed;
        }

        /// <summary>
        /// Returns <see cref="MovementParameters.None"/> when there are fewer than
        /// two samples, when the times are not strictly increasing, when a value
        /// is not a finite number, or when the speed is below <see cref="MinSpeed"/>.
        /// </summary>
        public MovementParameters Convert(IReadOnlyList<InputSample> samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samples.Count < 2 || !AreValid(samples))
            {
                return MovementParameters.None;
            }

            var first = samples[0];
            var last = samples[samples.Count - 1];
            var deltaX = last.X - first.X;
            var deltaY = last.Y - first.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var speed = distance / (last.TimeSeconds - first.TimeSeconds);

            if (speed < MinSpeed || distance == 0)
            {
                return MovementParameters.None;
            }

            return new MovementParameters(deltaX / distance, deltaY / distance, Math.Min(1.0, speed / MaxSpeed), true);
        }

        private static bool AreValid(IReadOnlyList<InputSample> samples)
        {
            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                if (!IsFinite(sample.X) || !IsFinite(sample.Y) || !IsFinite(sample.TimeSeconds))
                {
                    return false;
                }

                if (index > 0 && sample.TimeSeconds <= samples[index - 1].TimeSeconds)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
