using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HashReference
{
    // Credits Menu
    public static int _scrollCreditsTrigger = Animator.StringToHash("ScrollCredits");
    // Cutscene Manager and Player Select Canvas
    public static int _countdownTrigger = Animator.StringToHash("Countdown");
    // Becon Indicator
    public static int _spawnTrigger = Animator.StringToHash("Spawn");
    public static int _resetTrigger = Animator.StringToHash("Reset");
    // Number Handler
    public static int _shrinkTrigger = Animator.StringToHash("Shrink");
    public static int _growTrigger = Animator.StringToHash("Grow");
    public static int _idleTrigger = Animator.StringToHash("Idle");
    // Compass Icon UI
    public static int _fadeOutTrigger = Animator.StringToHash("FadeOut");
    public static int _fadeInTrigger = Animator.StringToHash("FadeIn");
    // Customization Selector
    public static int _slideLeftTrigger = Animator.StringToHash("SlideLeft");
    public static int _slideRightTrigger = Animator.StringToHash("SlideRight");
    // Results screen
    public static int _wipeInTrigger = Animator.StringToHash("WipeIn");
    public static int _wipeOutTrigger = Animator.StringToHash("WipeOut");
    public static int _startTrigger = Animator.StringToHash("Start");

    // Ghost
    public static int _speedFloat = Animator.StringToHash("Speed");
    public static int _endStatusFloat = Animator.StringToHash("End Status");
    public static int _stealLeftTrigger = Animator.StringToHash("StealLeft");

    public static int _mainTexProperty = Shader.PropertyToID("_MainTex");
    public static int _mainColorProperty = Shader.PropertyToID("_MainColor");
    public static int _normalMapProperty = Shader.PropertyToID("_NormalMap");
    // Vinette Render Feature
    public static int _radiusProperty = Shader.PropertyToID("_Radius");
    public static int _featherProperty = Shader.PropertyToID("_Feather");
    public static int _imageTexProperty = Shader.PropertyToID("_ImageTex");
    // Disolver
    public static int _mainTextureProperty = Shader.PropertyToID("_MainTexture");
    public static int _bumpMapProperty = Shader.PropertyToID("_BumpMap");
    public static int _baseColorProperty = Shader.PropertyToID("_BaseColor");
    public static int _cutoffHeightProperty = Shader.PropertyToID("_CutoffHeight");
    // Horn Color
    public static int _emissionColorProperty = Shader.PropertyToID("_EmissionColor");
    // Composite Pass
    public static int _texture1Property = Shader.PropertyToID("_Texture1");
    public static int _texture2Property = Shader.PropertyToID("_Texture2");
}
