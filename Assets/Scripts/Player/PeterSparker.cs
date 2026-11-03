using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PeterSparker : SingletonMonobehaviour<PeterSparker>
{
    [Tooltip("Reference to the prefab")]
    [SerializeField] private GameObject impactParticle;

    /// <summary>
    /// Creates sparks on the surface of a collider
    /// </summary>
    /// <param name="input">The collider</param>
    /// <param name="pos">The position of a point in the direction that the sparks should arrive relative to the collider's centre</param>
    public void CreateImpactFromCollider(Collider input, Vector3 pos)
    {
        Vector3 closestPoint = input.ClosestPoint(pos);
        CreateCollisionSparks(closestPoint);
    }

    public void CreateCollisionSparks(Vector3 sparkPoint)
    {
        GameObject hit = Instantiate(impactParticle, sparkPoint, Quaternion.identity);
        Destroy(hit, 1.5f);
    }

    /// <summary>How much bigger a clash's burst is than a bump's sparks</summary>
    internal const float CLASH_SCALE = 3f;

    /// <summary>
    /// The big burst between two boosting players who clashed (playtest, 2026-11-02: a clash looked like any bump): the
    /// sparks, CLASH_SCALE bigger, halfway between their balls. Every machine shows it once per clash
    /// </summary>
    public void CreateClashBurst(Vector3 ballA, Vector3 ballB)
    {
        GameObject burst = Instantiate(impactParticle, (ballA + ballB) / 2f + Vector3.up * 0.5f, Quaternion.identity);
        ScaleBurst(burst, CLASH_SCALE);
        Destroy(burst, 2f);
    }

    /// <summary>Scales a burst whole: its particles too, which by default keep their size whatever the scale</summary>
    internal static void ScaleBurst(GameObject burst, float scale)
    {
        foreach (ParticleSystem system in burst.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        burst.transform.localScale = Vector3.one * scale;
    }
}
