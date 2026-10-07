using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CivilianManager : MonoBehaviour
{
    [Header("Pedestrian Setup")]
    [Tooltip("How many civilians to create")]
    [SerializeField] private int pedestrianCount = 10;
    [Tooltip("Civilian prefab reference")]
    [SerializeField] private GameObject[] civilianPrefabs;
    [Tooltip("Civilian mayor prefab reference")]
    [SerializeField] private GameObject mayorPrefab;

    [Header("Waypoints")]
    [Tooltip("How many waypoints each *pedestrian* should have in their net")]
    [SerializeField] private int pedestrianWaypointCount = 4;

    private GameObject[] pedestrians;
    private List<Transform> waypoints;

    private void Start()
    {
        waypoints = new List<Transform>();
        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }

        pedestrians = new GameObject[pedestrianCount];

        for (int i = 0; i < pedestrianCount; i++)
        {
            Transform[] chosenPoints = new Transform[pedestrianWaypointCount];

            ShuffleWaypoints();
            for (int j = 0; j < pedestrianWaypointCount; j++)
            {
                chosenPoints[j] = waypoints[j];
            }

            GameObject pedestrian;

            if (i == 0)
                pedestrian = Instantiate(mayorPrefab, chosenPoints[0].position, Quaternion.identity);
            else
                pedestrian = Instantiate(civilianPrefabs[Random.Range(0, civilianPrefabs.Length)], chosenPoints[0].position, Quaternion.identity);

            CivilianAgent pedestrianAgent = pedestrian.GetComponent<CivilianAgent>();

            pedestrianAgent.Points = chosenPoints;
            pedestrianAgent.BeginPathing();
        }
    }

    /// <summary>
    /// Implements the Fisher-Yates shuffle to shuffle the list of waypoints
    /// </summary>
    private void ShuffleWaypoints()
    {
        for (int n = waypoints.Count - 1; n > 0; n--)
        {
            int k = Random.Range(0, n + 1);

            Transform temp = waypoints[n];
            waypoints[n] = waypoints[k];
            waypoints[k] = temp;
        }
    }
}
