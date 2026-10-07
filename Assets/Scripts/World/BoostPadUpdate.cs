using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoostPadUpdate : MonoBehaviour
{
    [SerializeField] bool enableUpdate;
    [SerializeField] Material referenceArrowMaterial;
    [SerializeField] MeshRenderer[] meshRenderers;

    [SerializeField] bool added = false;

    [SerializeField] float angle;
    [SerializeField] float extraAngle;

    private static readonly int RotateAxisId = Shader.PropertyToID("_RotateAxis");

    /// <summary>Only a turn bigger than this (degrees) is written to the arrow's material</summary>
    private const float ANGLE_EPSILON = 0.5f;

    private Material[] arrowMaterials; // each renderer's arrow material, found once (the materials getter allocates)
    private float[] lastAngles;        // the last angle written to each arrow

    private void Start()
    {
        CacheArrowMaterials();
    }

    private void CacheArrowMaterials()
    {
        arrowMaterials = new Material[meshRenderers.Length];
        lastAngles = new float[meshRenderers.Length];

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            arrowMaterials[i] = meshRenderers[i].materials[1];
            lastAngles[i] = float.NaN;
        }
    }

    private void Update()
    {
        if (enableUpdate == false)
            return;

        if (added == false)
        {
            added = true;
            BoostPadManager.Instance.AddBoostPad(this);
        }
    }

    public void UpdatePadRotation(GameObject playerPassin, int playerPosition)
    {
        var xDistance = playerPassin.transform.position.x - this.transform.position.x;
        var zDistance = playerPassin.transform.position.z - this.transform.position.z;

        angle = (Mathf.Atan2(zDistance, xDistance) * Mathf.Rad2Deg) + extraAngle;

        if (arrowMaterials == null)
            CacheArrowMaterials();

        // A tiny turn isn't worth a material write
        if (!float.IsNaN(lastAngles[playerPosition]) && Mathf.Abs(angle - lastAngles[playerPosition]) <= ANGLE_EPSILON)
            return;

        lastAngles[playerPosition] = angle;
        arrowMaterials[playerPosition].SetFloat(RotateAxisId, angle);
    }

}
