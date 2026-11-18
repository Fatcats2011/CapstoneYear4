using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UIElements;

public class MovingPlatform : MonoBehaviour
{
    //Variables
    [SerializeField] private Transform startingPos;
    [SerializeField] private Transform endPos;
    [SerializeField] private float speed = 1.0f;

    [SerializeField] private bool waitAtEnd = false;

    [SerializeField] private float secondsToWaitFor = 1;

    private IEnumerator pauseAtEndPointCoroutine;

    private Transform temp = null;

    private BallDriving[] activeScooters = { null, null, null, null };

    private void Start()
    {
        gameObject.transform.position = startingPos.position;
        StartPauseAtEndPoint();
    }

    private IEnumerator PauseAtEndPoint()
    {
        while (true)
        {
            Vector3 direction = (endPos.position - gameObject.transform.position).normalized;
            gameObject.transform.position = Vector3.MoveTowards(gameObject.transform.position, endPos.position, speed * Time.deltaTime);

            for (int i = 0; i < activeScooters.Length; i++) 
            {
                if (activeScooters[i] != null)
                {
                    activeScooters[i].Sphere.transform.position += (direction * speed * Time.deltaTime);
                    activeScooters[i].transform.position += (direction * speed * Time.deltaTime);
                }
            }

            if (transform.position == endPos.position)
            {
                temp = startingPos;
                startingPos = endPos;
                endPos = temp;

                if (waitAtEnd)
                {
                    yield return new WaitForSecondsRealtime(secondsToWaitFor);
                }
            }

            yield return null;
        }
    }

    public int AddToScooterList(BallDriving inp)
    {
        for (int i = 0; i < activeScooters.Length; i++)
        {
            if (activeScooters[i] == null)
            {
                activeScooters[i] = inp;
                return i;
            }
        }
        return -1;
    }
    public void RemoveFromScooterList(int index)
    {
        activeScooters[index] = null;
    }



    private void StartPauseAtEndPoint()
    {
        pauseAtEndPointCoroutine = PauseAtEndPoint();
        StartCoroutine(pauseAtEndPointCoroutine);
    }
    private void StopPauseAtEndPoint()
    {
        StopCoroutine(pauseAtEndPointCoroutine);
        pauseAtEndPointCoroutine = null;
    }
}
