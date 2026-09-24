using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkideeSkidoo : MonoBehaviour
{
    [SerializeField] private TrailRenderer frontTire;
    [SerializeField] private TrailRenderer backTire;

    private BallDriving control;

    private GameObject frontPos;
    private GameObject backPos;

    [Tooltip("Time the skidmarks last.")]
    [SerializeField] private float skidTime = 1f;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += StartSkidding;
        GameManager.Instance.OnSwapResults += StopSkidding;
    }
    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= StartSkidding;
        GameManager.Instance.OnSwapResults -= StopSkidding;
    }

    private void StartSkidding() { SetPoopy(skidTime); }
    private void StopSkidding() { SetPoopy(0); }

    private void Start()
    {
        control = GetComponent<BallDriving>();

        frontTire.alignment = LineAlignment.TransformZ;
        backTire.alignment = LineAlignment.TransformZ;

        frontTire.emitting = false;
        backTire.emitting = false;

        frontPos = new GameObject("Front Position");
        frontPos.transform.position = frontTire.transform.position;
        frontPos.transform.rotation = frontTire.transform.rotation;
        frontPos.transform.parent = transform;

        backPos = new GameObject("Back Position");
        backPos.transform.position = backTire.transform.position;
        backPos.transform.rotation = backTire.transform.rotation;
        backPos.transform.parent = transform;

        frontTire.transform.parent = null;
        backTire.transform.parent = null;
        frontTire.gameObject.layer = 0;
        backTire.gameObject.layer = 0;
    }

    private void Update()
    {
        frontTire.transform.position = frontPos.transform.position;
        frontTire.transform.rotation = frontPos.transform.rotation;
        backTire.transform.position = backPos.transform.position;
        backTire.transform.rotation = backPos.transform.rotation;

        frontTire.emitting = control.Grounded && control.Drifting;
        backTire.emitting = control.Grounded && control.Drifting;
    }

    /// <summary>
    /// Used for setting the time of skid marks.
    /// </summary>
    private void SetPoopy(float poopTime)
    {
        frontTire.time = poopTime;
        backTire.time = poopTime;
    }
}
