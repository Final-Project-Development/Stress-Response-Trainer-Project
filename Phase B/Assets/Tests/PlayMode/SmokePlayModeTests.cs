using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StressTrainer.Tests.PlayMode
{
    /// <summary>
    /// Smoke tests that exercise the PlayMode test pipeline (runtime loop, frame stepping).
    /// These run in an isolated test scene and never touch the real game scenes.
    /// </summary>
    public class SmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameObject_SurvivesAcrossFrames()
        {
            var go = new GameObject("StressTrainer_SmokeTest");
            yield return null; // advance one frame
            yield return null;

            Assert.IsNotNull(go, "Spawned object should still exist after frames advance.");
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator SciClassification_StaysStableAcrossFrames()
        {
            // Pure logic should behave identically inside the running player loop.
            float sci = StressChangeIndexCalculator.ComputeSciPercent(100f, 40f);
            var bandBefore = StressChangeIndexCalculator.Classify(sci);

            yield return new WaitForSeconds(0.1f);

            var bandAfter = StressChangeIndexCalculator.Classify(sci);
            Assert.AreEqual(StressChangeIndexCalculator.StressBand.High, bandAfter);
            Assert.AreEqual(bandBefore, bandAfter);
        }
    }
}
