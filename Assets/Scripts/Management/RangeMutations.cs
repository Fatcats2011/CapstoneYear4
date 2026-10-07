using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
/// <summary>
/// A helper class for mapping ranges of values to different ranges, which is commonly needed for the BallDriving script
/// </summary>
public static class RangeMutations
{
    /// <summary>
    /// Used for scaling steering based on speed. Takes values in a range from 0 to accelerationPower, then outputs values in a range from 0 to 1.
    /// Uses a parabolic function that basically makes it so that 0 maps to 0, 0.7 maps to 1, and 1 maps to about 0.85, all on a nice quadratic curve.
    /// Specifically uses the equation -(1.4x - 0.65)^2 + x + 0.408
    /// If anyone can find a better curve, let me know; this one doesn't quite hit 1 (highest it gets is 0.9998), and has a slight deadzone around 0
    /// </summary>
    /// <param name="inp">the input speed; aka the currentForce variable</param>
    /// <param name="maxSpeed">the max speed; aka the accelerationPower variable</param>
    /// <returns></returns>
    public static float Map_SpeedToSteering(float inp, float maxSpeed)
    {
        float processedValue = inp / maxSpeed;
        float numerator = 1.65f * processedValue;
        float denominator = Mathf.Pow(((2.0f * processedValue) -1),2) + 1.0f;
        processedValue = numerator / denominator; //shame on me, using division...

        return processedValue;
    }

    /// <summary>
    /// Remaps a value along a linear scale based on set parameters
    /// </summary>
    /// <param name="inp">The input value, which must lie between inMin and inMax</param>
    /// <param name="inMin">The lower bound of the initial scale</param>
    /// <param name="inMax">The upper bound of the initial scale</param>
    /// <param name="outMin">The lower bound of the modified scale</param>
    /// <param name="outMax">The upper bound of the modified scale</param>
    /// <returns></returns>
    public static float Map_Linear(float inp, float inMin, float inMax, float outMin, float outMax)
    {
        float processedValue = (inp - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;

        return processedValue;
    }
}
