using NUnit.Framework;

namespace StressTrainer.Tests.EditMode
{
    /// <summary>
    /// Unit tests for the core Stress Change Index algorithm:
    /// SCI = ((HRV_base - HRV_current) / HRV_base) * 100
    /// </summary>
    public class StressChangeIndexCalculatorTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void ComputeSci_CurrentHalfOfBaseline_Is50Percent()
        {
            float sci = StressChangeIndexCalculator.ComputeSciPercent(100f, 50f);
            Assert.AreEqual(50f, sci, Tolerance);
        }

        [Test]
        public void ComputeSci_CurrentEqualsBaseline_IsZero()
        {
            float sci = StressChangeIndexCalculator.ComputeSciPercent(80f, 80f);
            Assert.AreEqual(0f, sci, Tolerance);
        }

        [Test]
        public void ComputeSci_CurrentAboveBaseline_IsNegative()
        {
            // HRV rising above baseline (relaxation) yields a negative SCI.
            float sci = StressChangeIndexCalculator.ComputeSciPercent(100f, 120f);
            Assert.AreEqual(-20f, sci, Tolerance);
        }

        [TestCase(0f)]
        [TestCase(0.01f)]
        [TestCase(-5f)]
        public void ComputeSci_NonPositiveBaseline_ReturnsZeroGuard(float baseline)
        {
            // Guards against divide-by-zero / garbage when no baseline is established.
            float sci = StressChangeIndexCalculator.ComputeSciPercent(baseline, 40f);
            Assert.AreEqual(0f, sci, Tolerance);
        }

        [TestCase(0f, StressChangeIndexCalculator.StressBand.Low)]
        [TestCase(19.99f, StressChangeIndexCalculator.StressBand.Low)]
        [TestCase(20f, StressChangeIndexCalculator.StressBand.Moderate)]
        [TestCase(49.99f, StressChangeIndexCalculator.StressBand.Moderate)]
        [TestCase(50f, StressChangeIndexCalculator.StressBand.High)]
        [TestCase(120f, StressChangeIndexCalculator.StressBand.High)]
        [TestCase(-10f, StressChangeIndexCalculator.StressBand.Low)]
        public void Classify_MapsThresholdsToBands(float sci, StressChangeIndexCalculator.StressBand expected)
        {
            Assert.AreEqual(expected, StressChangeIndexCalculator.Classify(sci));
        }

        [TestCase(StressChangeIndexCalculator.StressBand.Low, "Low")]
        [TestCase(StressChangeIndexCalculator.StressBand.Moderate, "Moderate")]
        [TestCase(StressChangeIndexCalculator.StressBand.High, "High")]
        public void BandLabel_ReturnsHumanReadableText(StressChangeIndexCalculator.StressBand band, string expected)
        {
            Assert.AreEqual(expected, StressChangeIndexCalculator.BandLabel(band));
        }

        [Test]
        public void GetBandColor_FromPercent_MatchesColorFromBand()
        {
            // The float overload must agree with the band overload after classification.
            Assert.AreEqual(
                StressChangeIndexCalculator.GetBandColor(StressChangeIndexCalculator.StressBand.High),
                StressChangeIndexCalculator.GetBandColor(75f));

            Assert.AreEqual(
                StressChangeIndexCalculator.GetBandColor(StressChangeIndexCalculator.StressBand.Low),
                StressChangeIndexCalculator.GetBandColor(5f));
        }
    }
}
