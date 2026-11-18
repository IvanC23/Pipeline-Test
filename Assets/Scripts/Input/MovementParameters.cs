namespace StarCollector.InputConversion
{
    /// <summary>
    /// Game parameters obtained from a sequence of input samples.
    /// </summary>
    public readonly struct MovementParameters
    {
        /// <summary>No movement: invalid input or speed below the minimum.</summary>
        public static readonly MovementParameters None = new MovementParameters(0, 0, 0, false);

        /// <summary>Horizontal component of the unit direction (0 when not valid).</summary>
        public double DirectionX { get; }

        /// <summary>Vertical component of the unit direction (0 when not valid).</summary>
        public double DirectionY { get; }

        /// <summary>Speed scaled to the maximum speed, from 0 to 1.</summary>
        public double NormalizedSpeed { get; }

        /// <summary>True when the samples describe a movement the game should use.</summary>
        public bool IsValid { get; }

        public MovementParameters(double directionX, double directionY, double normalizedSpeed, bool isValid)
        {
            DirectionX = directionX;
            DirectionY = directionY;
            NormalizedSpeed = normalizedSpeed;
            IsValid = isValid;
        }
    }
}
