namespace StarCollector.InputConversion
{
    /// <summary>
    /// One simulated reading of the player's pointer: position on the play
    /// area and the time of the reading.
    /// </summary>
    public readonly struct InputSample
    {
        public double X { get; }
        public double Y { get; }
        public double TimeSeconds { get; }

        public InputSample(double x, double y, double timeSeconds)
        {
            X = x;
            Y = y;
            TimeSeconds = timeSeconds;
        }
    }
}
