using System;
using NUnit.Framework;
using StarCollector.Scoring;

namespace StarCollector.Tests.EditMode.Scoring
{
    public class ScoreCalculatorTests
    {
        private ScoreCalculator calculator;

        [SetUp]
        public void CreateCalculator()
        {
            // Target time 2 s, maximum time 10 s.
            calculator = new ScoreCalculator();
        }

        [Test]
        [Category("FUNC-01")]
        public void Calculate_WithinTargetTimeAndFullAccuracy_ReturnsMaxPoints(
            [Values(0.0, 1.0, 2.0)] double elapsedSeconds)
        {
            Assert.That(calculator.Calculate(elapsedSeconds, 1.0), Is.EqualTo(ScoreCalculator.MaxPointsPerStar));
        }

        [Test]
        [Category("FUNC-01")]
        public void Calculate_BetweenTargetAndMaxTime_DecreasesLinearly()
        {
            // Halfway between 2 s and 10 s the time factor is 0.5.
            Assert.That(calculator.Calculate(6.0, 1.0), Is.EqualTo(500));
            Assert.That(calculator.Calculate(8.0, 1.0), Is.EqualTo(250));
        }

        [Test]
        [Category("FUNC-01")]
        public void Calculate_AtOrAfterMaxTime_ReturnsZero([Values(10.0, 15.0)] double elapsedSeconds)
        {
            Assert.That(calculator.Calculate(elapsedSeconds, 1.0), Is.EqualTo(0));
        }

        [Test]
        [Category("FUNC-01")]
        public void Calculate_ScalesWithAccuracy()
        {
            Assert.That(calculator.Calculate(1.0, 0.25), Is.EqualTo(250));
            Assert.That(calculator.Calculate(6.0, 0.5), Is.EqualTo(250));
        }

        [Test]
        [Category("IO-01")]
        public void Calculate_AccuracyLimits_AreAccepted()
        {
            Assert.That(calculator.Calculate(0.0, 0.0), Is.EqualTo(0));
            Assert.That(calculator.Calculate(0.0, 1.0), Is.EqualTo(ScoreCalculator.MaxPointsPerStar));
        }

        [TestCase(-0.01)]
        [TestCase(1.01)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [Category("IO-01")]
        public void Calculate_AccuracyOutOfRange_Throws(double accuracy)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(1.0, accuracy));
        }

        [TestCase(-0.001)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [Category("IO-01")]
        public void Calculate_InvalidElapsedTime_Throws(double elapsedSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(elapsedSeconds, 1.0));
        }

        [TestCase(0.0, 10.0)]
        [TestCase(-1.0, 10.0)]
        [TestCase(5.0, 5.0)]
        [TestCase(5.0, 4.0)]
        [TestCase(double.NaN, 10.0)]
        [TestCase(2.0, double.PositiveInfinity)]
        [Category("IO-01")]
        public void Constructor_InvalidTimes_Throws(double targetTimeSeconds, double maxTimeSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreCalculator(targetTimeSeconds, maxTimeSeconds));
        }
    }
}
