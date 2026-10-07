using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvironmentSwapper : MonoBehaviour
{
    [SerializeField] private GameObject finalMap;

    public void SwapToFinal()
    {
        finalMap.SetActive(true);
    }
}
