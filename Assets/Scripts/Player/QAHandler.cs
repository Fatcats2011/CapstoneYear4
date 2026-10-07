using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is for gathering player information and sending it to the QA manager. Attach to control on player prefab.
/// </summary>
public class QAHandler : MonoBehaviour
{
    private OrderHandler orderHandler;
    
    // raw stats
    private int easy = 0, med = 0, hard = 0, gold = 0; // number of each package type delivered

    private int boosts = 0, steals = 0, goldSteals = 0, deaths = 0; // counters for different gameplay functions

    public int Boosts { get { return boosts; } set {  boosts = value; } }
    public int Steals { get { return steals; } set {  steals = value; } }
    public int Deaths { get { return deaths; } set {  deaths = value; } }
    public int GoldSteals { get { return goldSteals; } set { goldSteals = value; } }

    // heat map
    private GameObject trailObject;
    [Tooltip("How often trail objects are created.")]
    [SerializeField] private float trailFrequency = 1f;
    private bool shouldTrail = false;
    private float trailTimer = 0f;
    [Tooltip("Height above the player the icons are generated.")]
    [SerializeField] private float iconHeight = 5f;
    [Tooltip("Reference to the icon used to display death.")]
    [SerializeField] private GameObject deathIcon;
    [Tooltip("Reference to the parent GameObject of the heatmap sprites.")]
    [SerializeField] private GameObject trailParent;

    private void Start()
    {
        orderHandler = GetComponent<OrderHandler>();
        QAManager.Instance.AddHandler(this);
    }

    private void Update()
    {
        return;
        if (!shouldTrail || !QAManager.Instance.GenerateHeatmap) { return; }

        if(trailTimer > trailFrequency)
        {
            Instantiate(trailObject, this.transform.position + iconHeight * transform.up, 
                this.transform.rotation * Quaternion.Euler(90,1,1), trailParent.transform);
            trailTimer = 0f;
        }

        trailTimer += Time.deltaTime;
    }

    public void SetTrailObj(GameObject inTrail)
    {
        trailObject = inTrail;
        shouldTrail = true;
    }

    public void ResetQA()
    {
        easy = 0;
        med = 0;
        hard = 0;
        gold = 0;

        boosts = 0;
        steals = 0;
        goldSteals = 0;
        deaths = 0;
    }

    public string[] GetData()
    {
        string[] data = {
            this.gameObject.transform.parent.name,
            orderHandler.Placement.ToString(),
            orderHandler.Score.ToString(),
            easy.ToString(), med.ToString(), hard.ToString(), gold.ToString(), OrderManager.Instance.FinalOrderValue.ToString(),
            deaths.ToString(), boosts.ToString(), steals.ToString(), goldSteals.ToString()
        };

        return data;
    }

    public void Deliver(Constants.OrderValue value)
    {
        switch(value)
        {
            case Constants.OrderValue.Easy:
                easy++;
                break;
            case Constants.OrderValue.Medium:
                med++;
                break;
            case Constants.OrderValue.Hard: 
                hard++; 
                break;
            case Constants.OrderValue.Golden:
                gold++; 
                break;
        }
    }
}
