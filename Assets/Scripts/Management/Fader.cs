using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Can be attached to an object to fade it out.
/// </summary>
public class Fader : MonoBehaviour
{
    private Material mat;
    private Color originalColor;

    private IEnumerator changeFadeCoroutine;

    [Tooltip("Reference to the associated Renderer")]
    [SerializeField] private Renderer render;

    /// <summary>
    /// Stock Start. Gets some quick references.
    /// </summary>
    private void Start()
    {
        mat = render.material;

        // Some materials have no colour (the golden round's dissolving kickables): reading it logs an error, not an exception
        if (mat.HasProperty("_Color"))
            originalColor = mat.color;
    }

    /// <summary>
    /// Fades the material color's alpha down to 0 over param time.
    /// </summary>
    /// <param name="time">How long the fade takes</param>
    /// <returns>Boilerplate IEnumerator</returns>
    private IEnumerator ChangeFade(float time)
    {
        Color targetColor = new Color(mat.color.r, mat.color.g, mat.color.b, 0f);
        float elapsedTime = 0f;

        while (elapsedTime < time)
        {
            Color lerpedColor = Color.Lerp(originalColor, targetColor, Mathf.Clamp((elapsedTime / time), 0, 1));
            mat.color = lerpedColor;
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
    }

    /// <summary>
    /// Fade out the object over param time.
    /// </summary>
    /// <param name="time">How long the fade takes</param>
    public void FadeOut(float time)
    {
        StartChangeFade(time);
    }

    /// <summary>
    /// Reset the object to full opacity
    /// </summary>
    public void Reset()
    {
        mat.color = originalColor;
    }

    private void StartChangeFade(float time) 
    {
        changeFadeCoroutine = ChangeFade(time);
        StartCoroutine(changeFadeCoroutine);
    }
    private void StopChangeFade()
    {
        if (changeFadeCoroutine != null)
        {
            StopCoroutine(changeFadeCoroutine);
            changeFadeCoroutine = null;
        }
    }
}
