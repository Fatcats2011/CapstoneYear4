using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("UI/SliderBar", 34)]
public class SliderBar : MonoBehaviour
{
    /// <summary>
    /// The rect fill object
    /// </summary>
    [SerializeField] RectTransform fill;

    /// <summary>
    /// The rect fill mask for the fill object
    /// </summary>
    [SerializeField] RectTransform fillMask;

    /// <summary>
    /// The slider value for the slider
    /// </summary>
    [Range(0f, 1f)] public float value;

    /// <summary>
    /// Event type used by the UI.Slider.
    /// </summary>
    [Serializable] public class SliderEvent : UnityEvent<float> { }
    [SerializeField] private SliderEvent m_OnValueChanged = new SliderEvent();
    public SliderEvent onValueChanged { get { return m_OnValueChanged; } set { m_OnValueChanged = value; } }

    // private variables
    RectTransform fillRect;
    Vector2 maskSize;
    Vector2 fillSize;

    float fillWidth;

    public void Start()
    {
        maskSize = new Vector2(fillMask.GetComponent<RectTransform>().rect.width, fillMask.GetComponent<RectTransform>().rect.height);
        fillSize = new Vector2(fill.GetComponent<RectTransform>().rect.width, fill.GetComponent<RectTransform>().rect.height);
        fillRect = fill.GetComponent<RectTransform>();
    }

    public void Update()
    {
        fillWidth = RangeMutations.Map_Linear(value, 0, 1, 0, maskSize.x);
        fillRect.sizeDelta = new Vector2(fillWidth, fillSize.y);
    }

}