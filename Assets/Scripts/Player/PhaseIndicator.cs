using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using System.Numerics;

public class PhaseIndicator : MonoBehaviour
{
    bool initalized = false;

    [SerializeField] SliderBar hornSliderLeft;
    [SerializeField] SliderBar hornSliderRight;
    [SerializeField] float intensity = 3;
    [SerializeField] float readyIntensityBonus = 1.5f;

    [Range(0f, 1f)]
    [SerializeField] float hornGlowValue = 0;
    private float hornValueMax = 1f;
    float hornGlowStep;

    [SerializeField] Color readyColor;
    [SerializeField] Gradient hornGlowGraident;

    [SerializeField] BallDriving control;
  
    [Header("Material Info")]
    [SerializeField] Renderer ghostRenderer;
    [SerializeField] Material hornGlowRef;
    [SerializeField] Material hornGlow;

    [Header("Flashyflash")]
    [SerializeField] Transform leftHorn;
    [SerializeField] Transform rightHorn;
    [SerializeField] GameObject flashParticles;

    private SoundPool soundPool; // for playing SFX
    private bool dirtyBoostReady = true;

    public bool ShowPhase = false;

    Coroutine hornStatus;

    private const float RATIO_EPSILON = 0.0001f;
    private float lastRatio = float.NaN; // the gauge ratio last drawn on the sliders and horns

    private float currentBoostMaxTime;
    private float currentBoostRechargeAmount;

    // Define a delegate for the completion of the glow depletion
    public delegate void GlowDepleteComplete();

    private void Start()
    {
        soundPool = GetComponent<SoundPool>();

        hornGlowStep = hornValueMax / 100;
    }

    private void Update()
    {
        currentBoostMaxTime = control.BoostTimerMaxTime; //Gets the current values of boost recharge status from balldriving
        currentBoostRechargeAmount = control.BoostElapsedTime;

        SetHornColor(currentBoostRechargeAmount, currentBoostMaxTime);
    }

    /// <summary>
    /// Sets the reference to the horn glow, is called during during customization to correctly reference horns with player materials
    /// </summary>
    public void ReferenceHornMaterial()
    {
        hornGlow = new Material(hornGlowRef);

        float factor = Mathf.Pow(2, (1 + intensity));

        Color color = new Color(readyColor.r * factor, readyColor.g * factor, readyColor.b * factor);

        hornGlow.SetColor(HashReference._baseColorProperty, readyColor);
        hornGlow.SetColor(HashReference._emissionColorProperty, color);

        Material[] ghostMaterials = ghostRenderer.materials;
        ghostMaterials[1] = hornGlow;

        ghostRenderer.materials = ghostMaterials;

        lastRatio = float.NaN; // the new horn material needs drawing again

        initalized = true;
    }

    /// <summary>
    /// Whether the horn needs drawing for a new gauge ratio: not for a change too small to see, but always when it reaches
    /// or leaves full (the boost-ready cue and colour) or empty, and the first time
    /// </summary>
    public static bool NeedsRedraw(float ratio, float lastRatio)
    {
        if (float.IsNaN(lastRatio))
            return true;
        if ((ratio >= 1f) != (lastRatio >= 1f) || (ratio <= 0f) != (lastRatio <= 0f))
            return true;

        return Mathf.Abs(ratio - lastRatio) >= RATIO_EPSILON;
    }

    /// <summary>
    /// Sets the horn color value
    /// </summary>
    public void SetHornColor(float passInCurrent, float passInMax)
    {
        float ratio = passInCurrent / passInMax;

        // Nothing to redraw while the gauge hasn't moved
        if (!NeedsRedraw(ratio, lastRatio))
            return;
        lastRatio = ratio;

        hornSliderLeft.value = ratio;
        hornSliderRight.value = ratio;
        hornGlowValue = ratio * hornValueMax;

        float intensityFactor = Mathf.Pow(2, (hornGlowValue * intensity));

        // Ready boost color
        if (ratio >= 1f)
        {
            if (!dirtyBoostReady)
            {
                soundPool.PlayBoostReady();
                FlashHorns();
                dirtyBoostReady = true;
            }

            Color color = new Color(readyColor.r * readyIntensityBonus * intensityFactor, readyColor.g * readyIntensityBonus * intensityFactor, readyColor.b * readyIntensityBonus * intensityFactor);
            hornGlow.SetColor(HashReference._emissionColorProperty, color);
            hornGlow.SetColor(HashReference._baseColorProperty, readyColor);
        }
        else
        {
            dirtyBoostReady = false;
            Color currentColor = (hornGlowGraident.Evaluate(hornGlowValue));
            Color color = new Color(currentColor.r * intensityFactor, currentColor.g * intensityFactor, currentColor.b * intensityFactor);
            hornGlow.SetColor(HashReference._emissionColorProperty, color);
            hornGlow.SetColor(HashReference._baseColorProperty, currentColor);
        }
    }

    /// <summary>
    /// Flashes both horns: the boost gauge is full. Online, another machine's scooter flashes when its owner's gauge fills
    /// (OnlineCues)
    /// </summary>
    public void FlashHorns()
    {
        CreateFlash(leftHorn.transform.position);
        CreateFlash(rightHorn.transform.position);
    }

    private void CreateFlash(UnityEngine.Vector3 location)
    {
        GameObject grandmasterFlash = Instantiate(flashParticles, location, UnityEngine.Quaternion.identity);
        grandmasterFlash.transform.parent = this.transform;
        Destroy(grandmasterFlash, 1.5f);
    }
}
