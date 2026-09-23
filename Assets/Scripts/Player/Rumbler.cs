using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Rumbler : MonoBehaviour
{
    private IEnumerator pulseTimeCoroutine;

    private bool suspendedInEffect = false;
    private float suspendedLow = 0;
    private float suspendedHigh = 0;

    private void Start()
    {
        InputSystem.ResetHaptics();
    }

    private void OnApplicationQuit()
    {
        InputSystem.ResetHaptics();
    }

    public void SuspendedRumble(Gamepad pad, float low, float high, bool replace = true)
    {
        if (suspendedInEffect && !replace)
            return;

        suspendedLow = low;
        suspendedHigh = high;

        pad?.SetMotorSpeeds(low, high);
        suspendedInEffect = true;
    }

    public void EndSuspension(Gamepad pad)
    {
        suspendedHigh = 0;
        suspendedLow = 0;
        pad?.SetMotorSpeeds(0f, 0f);
        suspendedInEffect = false;
    }

    public void RumblePulse(Gamepad pad, float low, float high, float duration, bool breakSuspension = false)
    {
        if (pulseTimeCoroutine != null)
        {
            StopPulseTime();
        }

        if (pad != null)
        {
            pad.SetMotorSpeeds(low, high);
        }

        StartPulseTime(duration, pad, breakSuspension);
    }

    private IEnumerator PulseTime(float duration, Gamepad pad, bool breakSuspension)
    {
        yield return new WaitForSeconds(duration);
        pad?.SetMotorSpeeds(breakSuspension ? 0f : suspendedLow, breakSuspension ? 0f : suspendedHigh);
    }

    private void StartPulseTime(float duration, Gamepad pad, bool breakSuspension)
    {
        pulseTimeCoroutine = PulseTime(duration, pad, breakSuspension);
        StartCoroutine(pulseTimeCoroutine);
    }
    private void StopPulseTime()
    {
        if (pulseTimeCoroutine != null)
        {
            StopCoroutine(pulseTimeCoroutine);
            pulseTimeCoroutine = null;
        }
    }
}
