using NUnit.Framework;

namespace StressTrainer.Tests.EditMode
{
    /// <summary>
    /// Unit tests for SimulationRunOutcome value clamping and pace classification.
    /// </summary>
    public class SimulationRunOutcomeTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void Create_ClampsNegativeAndOutOfRangeValues()
        {
            var outcome = SimulationRunOutcome.Create(
                elapsedSeconds: -10f,
                timeLimitSeconds: -5f,
                missionCompleted: true,
                timedOut: false,
                completionRatio: 1.5f,
                highStressSeconds: -3f,
                taskStrikeCount: -2);

            Assert.AreEqual(0f, outcome.elapsedSeconds, Tolerance, "elapsed should clamp to >= 0");
            Assert.AreEqual(0f, outcome.timeLimitSeconds, Tolerance, "time limit should clamp to >= 0");
            Assert.AreEqual(1f, outcome.completionRatio, Tolerance, "ratio should clamp to <= 1");
            Assert.AreEqual(0f, outcome.highStressSeconds, Tolerance, "high-stress seconds should clamp to >= 0");
            Assert.AreEqual(0, outcome.taskStrikeCount, "strike count should clamp to >= 0");
        }

        [Test]
        public void Create_NullReason_BecomesEmptyAndTrimmed()
        {
            var nullReason = SimulationRunOutcome.Create(10f, 60f, true, false, 1f);
            Assert.AreEqual(string.Empty, nullReason.disqualificationReason);

            var spacedReason = SimulationRunOutcome.Create(
                10f, 60f, false, false, 0.5f, disqualificationReason: "  too slow  ");
            Assert.AreEqual("too slow", spacedReason.disqualificationReason);
        }

        [Test]
        public void WasFast_TrueWhenCompletedUnderHalfTheLimit()
        {
            var outcome = SimulationRunOutcome.Create(30f, 100f, missionCompleted: true, timedOut: false, completionRatio: 1f);
            Assert.IsTrue(outcome.WasFast);
            Assert.IsFalse(outcome.WasSlow);
        }

        [Test]
        public void WasSlow_TrueWhenCompletedNearTheLimit()
        {
            var outcome = SimulationRunOutcome.Create(90f, 100f, missionCompleted: true, timedOut: false, completionRatio: 1f);
            Assert.IsTrue(outcome.WasSlow);
            Assert.IsFalse(outcome.WasFast);
        }

        [Test]
        public void WasFastAndWasSlow_FalseWhenMissionNotCompleted()
        {
            var outcome = SimulationRunOutcome.Create(10f, 100f, missionCompleted: false, timedOut: true, completionRatio: 0.3f);
            Assert.IsFalse(outcome.WasFast);
            Assert.IsFalse(outcome.WasSlow);
        }

        [Test]
        public void WasFastAndWasSlow_FalseWhenNoTimeLimit()
        {
            var outcome = SimulationRunOutcome.Create(10f, 0f, missionCompleted: true, timedOut: false, completionRatio: 1f);
            Assert.IsFalse(outcome.WasFast);
            Assert.IsFalse(outcome.WasSlow);
        }
    }
}
