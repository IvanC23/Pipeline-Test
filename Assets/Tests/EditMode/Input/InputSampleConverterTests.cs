using System;
using NUnit.Framework;
using StarCollector.InputConversion;

namespace StarCollector.Tests.EditMode.InputConversion
{
    public class InputSampleConverterTests
    {
        private const double Tolerance = 1e-9;

        private InputSampleConverter converter;

        [SetUp]
        public void CreateConverter()
        {
            // Maximum speed 20 units/s, minimum speed 0.5 units/s.
            converter = new InputSampleConverter();
        }

        [Test]
        [Property("Requirement", "IO-02")]
        public void Convert_StraightMovement_ReturnsUnitDirectionAndScaledSpeed()
        {
            // 5 units in 1 second: speed 5, a quarter of the maximum speed.
            var result = converter.Convert(new[]
            {
                new InputSample(0, 0, 0),
                new InputSample(1.5, 2, 0.5),
                new InputSample(3, 4, 1),
            });

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.DirectionX, Is.EqualTo(0.6).Within(Tolerance));
            Assert.That(result.DirectionY, Is.EqualTo(0.8).Within(Tolerance));
            Assert.That(result.NormalizedSpeed, Is.EqualTo(0.25).Within(Tolerance));
        }

        [Test]
        [Property("Requirement", "IO-02")]
        public void Convert_FasterThanMaxSpeed_ClampsSpeedToOne()
        {
            var result = converter.Convert(new[] { new InputSample(0, 0, 0), new InputSample(100, 0, 1) });

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.NormalizedSpeed, Is.EqualTo(1.0));
        }

        [Test]
        [Property("Requirement", "IO-02")]
        public void Convert_FewerThanTwoSamples_ReturnsNone()
        {
            AssertNone(converter.Convert(new InputSample[0]));
            AssertNone(converter.Convert(new[] { new InputSample(1, 1, 0) }));
        }

        [TestCase(0.0)]
        [TestCase(-0.5)]
        [Property("Requirement", "IO-02")]
        public void Convert_TimesNotIncreasing_ReturnsNone(double secondSampleTime)
        {
            AssertNone(converter.Convert(new[] { new InputSample(0, 0, 0), new InputSample(5, 0, secondSampleTime) }));
        }

        [TestCase(double.NaN, 0.0, 1.0)]
        [TestCase(0.0, double.PositiveInfinity, 1.0)]
        [TestCase(5.0, 0.0, double.NaN)]
        [Property("Requirement", "IO-02")]
        public void Convert_ValueNotFinite_ReturnsNone(double x, double y, double timeSeconds)
        {
            AssertNone(converter.Convert(new[] { new InputSample(0, 0, 0), new InputSample(x, y, timeSeconds) }));
        }

        [Test]
        [Property("Requirement", "IO-02")]
        public void Convert_BelowMinSpeed_ReturnsNone()
        {
            AssertNone(converter.Convert(new[] { new InputSample(0, 0, 0), new InputSample(0.1, 0, 1) }));
        }

        [Test]
        [Property("Requirement", "IO-02")]
        public void Convert_NoDisplacement_ReturnsNoneEvenWithZeroMinSpeed()
        {
            var noMinimum = new InputSampleConverter(maxSpeed: 20.0, minSpeed: 0.0);

            AssertNone(noMinimum.Convert(new[] { new InputSample(1, 1, 0), new InputSample(1, 1, 1) }));
        }

        [Test]
        [Property("Requirement", "IO-02")]
        public void Convert_NullSamples_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => converter.Convert(null));
        }

        [TestCase(0.0, 0.0)]
        [TestCase(-1.0, 0.0)]
        [TestCase(double.NaN, 0.0)]
        [TestCase(double.PositiveInfinity, 0.0)]
        [TestCase(20.0, -1.0)]
        [TestCase(20.0, double.NaN)]
        [TestCase(20.0, 20.0)]
        [Property("Requirement", "IO-02")]
        public void Constructor_InvalidSpeeds_Throws(double maxSpeed, double minSpeed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InputSampleConverter(maxSpeed, minSpeed));
        }

        private static void AssertNone(MovementParameters result)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.DirectionX, Is.EqualTo(0.0));
            Assert.That(result.DirectionY, Is.EqualTo(0.0));
            Assert.That(result.NormalizedSpeed, Is.EqualTo(0.0));
        }
    }
}
