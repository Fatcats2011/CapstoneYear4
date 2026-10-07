using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dissolver : MonoBehaviour
{
    [SerializeField] private Material dissolverMatReference;
    Material dissolveMaterial;
    private Material oldMat;
    private Renderer render;

    private Rigidbody rb;

    private float objectHeight;

    private IEnumerator dissolveCoroutine;

    private void Start()
    {
        render = GetComponentInChildren<Renderer>();
        oldMat = render.material;
        rb = GetComponent<Rigidbody>();

        dissolveMaterial = new Material(dissolverMatReference);
        dissolveMaterial.SetTexture(HashReference._mainTextureProperty, oldMat.GetTexture(HashReference._mainTexProperty));
        dissolveMaterial.SetTexture(HashReference._normalMapProperty, oldMat.GetTexture(HashReference._bumpMapProperty));
        dissolveMaterial.SetColor(HashReference._mainColorProperty, oldMat.GetColor(HashReference._baseColorProperty));

        render.material = dissolveMaterial;
        objectHeight = render.bounds.size.y;
        render.material.SetFloat(HashReference._cutoffHeightProperty, 2048);
    }

    public void DissolveOut(float time)
    {
        StopDissolve();
        objectHeight = render.bounds.size.y;
        render.material.SetFloat(HashReference._cutoffHeightProperty, objectHeight + rb.position.y + 0.1f);
        StartDissolve(false, time);
    }

    public void DissolveIn(float time) 
    {
        StopDissolve();
        objectHeight = render.bounds.size.y;
        render.material.SetFloat(HashReference._cutoffHeightProperty, 0);
        StartDissolve(true, time);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="direction">True = up (filling in), False = down (disappearing)</param>
    /// <returns></returns>
    private IEnumerator Dissolve(bool direction, float time)
    {
        float height = direction ? 0 : objectHeight;
        float modifier = objectHeight / time;

        if (direction)
        {
            while (height < objectHeight)
            {
                height += (Time.deltaTime * modifier);
                render.material.SetFloat(HashReference._cutoffHeightProperty, height + rb.position.y);
                yield return null;
            }
            render.material.SetFloat(HashReference._cutoffHeightProperty, 2048);
        }
        else
        {
            while (height > -objectHeight)
            {
                height -= (Time.deltaTime * modifier);
                render.material.SetFloat(HashReference._cutoffHeightProperty, height + rb.position.y);
                yield return null;
            }
            render.material.SetFloat(HashReference._cutoffHeightProperty, 0);
        }
    }


    private void StartDissolve(bool direction, float time)
    {
        dissolveCoroutine = Dissolve(direction, time);
        StartCoroutine(dissolveCoroutine);
    }
    private void StopDissolve()
    {
        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
            dissolveCoroutine = null;
        }
    }
}
