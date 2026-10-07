using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ticker : MonoBehaviour
{
    public static float tickTime_1 = 0.5f;
    private float tickerTimer_1;

    public delegate void TickAction1();
    public static event TickAction1 OnTickAction010;

    // Update is called once per frame
    void Update()
    {
        tickerTimer_1 += Time.deltaTime;

        if(tickerTimer_1 >= tickTime_1)
        {
            tickerTimer_1 = 0;
            TickEvent();
        }
    }

    private void TickEvent()
    {
        OnTickAction010?.Invoke();
    }
}
