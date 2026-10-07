using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class handles the trail renders for boosting and eventually drifting.
/// </summary>
public class TrailHandler : MonoBehaviour
{
    private BallDriving ball;
    [Tooltip("Whatever trails you want enabled during the boost.")]
    [SerializeField] private TrailRenderer[] boostTrails;
    private float[] trailTime;

    [Tooltip("Speed at which the boost trails shrink and grow.")]
    [SerializeField] private float boostTrailMulitplier = 1f;

    // Start is called before the first frame update
    void Start()
    {
        ball = GetComponent<BallDriving>();
        trailTime = new float[boostTrails.Length];
        for(int i=0;i<boostTrails.Length; i++)
        {
            trailTime[i] = boostTrails[i].time;
            boostTrails[i].time = 0;
            boostTrails[i].gameObject.SetActive(false);
        }
        ball.OnBoostStart += GrowBoostTrail;
    }

    /// <summary>
    /// This method will start the coroutine to grow the boost trail.
    /// </summary>
    public void GrowBoostTrail()
    {
        for(int i=0;i<boostTrails.Length; i++)
        {
            StartCoroutine(LerpBoost(boostTrails[i], trailTime[i]));
        }
    }

    /// <summary>
    /// This coroutine will grow and shrink the trail.
    /// </summary>
    /// <param name="trail">Trail to be grown and shrunk</param>
    /// <param name="maxTime">Maximum time value to the grown to</param>
    /// <returns></returns>
    private IEnumerator LerpBoost(TrailRenderer trail, float maxTime)
    {
        trail.gameObject.SetActive(true);
        float startTime = trail.time;
        float elapsedTime = 0;
        while (trail.time < maxTime)
        {
            trail.time = Mathf.Lerp(startTime, maxTime, elapsedTime);
            elapsedTime += Time.deltaTime * boostTrailMulitplier;
            yield return null;
        }
        elapsedTime = 0;
        while(ball.Boosting)
        {
            yield return null;
        }

        while(trail.time > 0)
        {
            trail.time = Mathf.Lerp(maxTime, startTime, elapsedTime);
            elapsedTime += Time.deltaTime * boostTrailMulitplier;
            yield return null;
        }
        trail.gameObject.SetActive(false);
    }
}
