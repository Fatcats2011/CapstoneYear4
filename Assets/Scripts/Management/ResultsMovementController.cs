using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResultsMovementController : MonoBehaviour
{
    private List<GameObject> players = new List<GameObject>();
    [SerializeField] private Transform[] playerSpawns;
    [SerializeField] private GameObject enableThisForResults;
    [SerializeField] private GameObject disableThisForResults;
    [SerializeField] private float timePerBoost = 1f;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapResults += SpawnPlayers;
    }
    private void OnDisable()
    {
        GameManager.Instance.OnSwapResults -= SpawnPlayers;
    }

    private void SortPlayers()
    {
        players.Clear();
        for(int i=0;i<PlayerInstantiate.Instance.PlayerCount;i++)
        {
            try
            {
                players.Add(ScoreManager.Instance.GetHandlerOfIndex(i).transform.parent.GetComponentInChildren<Rigidbody>().gameObject); // super scuffed way to get ref to the sphere
            }
            catch
            {
                continue;
            }
        }
    }

    private void SpawnPlayers()
    {
        SortPlayers();
        enableThisForResults.SetActive(true);
        disableThisForResults.SetActive(false);
        for(int i=0;i<players.Count;i++)
        {
            // set position of the ball
            players[i].transform.position = playerSpawns[i].position;
            players[i].transform.rotation = Quaternion.identity;// playerSpawns[i].rotation;

            // rotate the control object
            players[i].transform.parent.GetComponentInChildren<BallDriving>().transform.rotation = Quaternion.identity; 
        }
    }
}
