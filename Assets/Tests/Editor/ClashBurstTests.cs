using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>The burst where two boosting players clash (PeterSparker.CreateClashBurst)</summary>
    public class ClashBurstTests
    {
        [Test]
        public void ScaleBurst_GrowsTheParticlesToo()
        {
            GameObject burst = new GameObject("Burst");
            GameObject child = new GameObject("Sparks");
            child.transform.SetParent(burst.transform);
            ParticleSystem sparks = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = sparks.main;
            main.scalingMode = ParticleSystemScalingMode.Local;
            try
            {
                PeterSparker.ScaleBurst(burst, PeterSparker.CLASH_SCALE);
                Assert.AreEqual(Vector3.one * PeterSparker.CLASH_SCALE, burst.transform.localScale);
                Assert.AreEqual(ParticleSystemScalingMode.Hierarchy, sparks.main.scalingMode, "the particles grow with it");
            }
            finally
            {
                Object.DestroyImmediate(burst);
            }
        }
    }
}
