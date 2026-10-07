using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(Camera))]
public class HeatmapCamera : MonoBehaviour
{
    [Tooltip("Position for the main map heat map")]
    [SerializeField] private Transform mainMapPos;
    [Tooltip("Position for the final map heat map")]
    [SerializeField] private Transform finalMapPos;
    private Camera viewport;
    private string screenshotNum = "";
    private string heatmapFolder = "InBuilds";

    private void Awake()
    {
        viewport = GetComponent<Camera>();
        viewport.enabled = false;

        if(Application.isEditor)
        {
            heatmapFolder = "InEngine";
        }
    }

    private void TakePicture()
    {

        if (!QAManager.Instance.GenerateHeatmap)
            return;

        viewport.enabled = true;

        this.transform.position = mainMapPos.position;
        this.transform.rotation = mainMapPos.rotation;

        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        viewport.targetTexture = rt;

        Texture2D image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        viewport.Render();

        RenderTexture.active = rt;
        image.ReadPixels(new Rect(0, 0, viewport.targetTexture.width, viewport.targetTexture.height), 0, 0);
        image.Apply();

        byte[] bytes = image.EncodeToPNG();
        Destroy(image);

        screenshotNum = DateTime.Now.ToString("Mddhhmmff");
        File.WriteAllBytes(Application.streamingAssetsPath + $"/HeatMaps/{heatmapFolder}/{screenshotNum}_main.png", bytes);
        viewport.enabled = false;

    }

    private void TakeFinalPicture()
    {
        if (!QAManager.Instance.GenerateHeatmap)
            return;

        viewport.enabled = true;

        this.transform.position = finalMapPos.position;
        this.transform.rotation = finalMapPos.rotation;

        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        viewport.targetTexture = rt;

        Texture2D image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        viewport.Render();

        RenderTexture.active = rt;
        image.ReadPixels(new Rect(0, 0, viewport.targetTexture.width, viewport.targetTexture.height), 0, 0);
        image.Apply();

        RenderTexture.active = null;
        viewport.targetTexture = null;

        byte[] bytes = image.EncodeToPNG();
        Destroy(image);

        if(screenshotNum == "")
            screenshotNum = DateTime.Now.ToString("Mddhhmmff");

        File.WriteAllBytes(Application.streamingAssetsPath + $"/HeatMaps/{heatmapFolder}/{screenshotNum}_final.png", bytes);

        screenshotNum = "";

        viewport.enabled = false;
    }
}
