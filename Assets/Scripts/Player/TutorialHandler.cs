using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TutorialHandler : MonoBehaviour
{
    private bool hasLearnt = false;
    public bool HasLearnt { get { return hasLearnt; } }

    public delegate void LearntDelegate();
    public LearntDelegate OnTutorialized;

    private BallDriving ball;

    [Tooltip("Boost modifier when the player is being tutorialized.")]
    [SerializeField] private float tutorialBoostMod = 0.1f;
    [Tooltip("Text shown on the driving canvas when the player is being tutorialized.")]
    [SerializeField] private TextMeshProUGUI tutorialText;
    [SerializeField] private TextMeshProUGUI tutorialTextPurple;

    [Header("Tutorial Text")]
    [SerializeField] private string boostInstructions;
    [SerializeField] private string boostInstructionsPurple;
    [SerializeField] private string pickupInstructions;
    [SerializeField] private string stealInstructions;
    [SerializeField] private string dropoffInstructions;
    [SerializeField] private GameObject tutorialImage;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += ResetHandler;
        GameManager.Instance.OnSwapGoldenCutscene += FinishTutorial;
        TutorialManager.Instance.OnTutorialComplete += FinishTutorial;
    }
    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= ResetHandler;
        GameManager.Instance.OnSwapGoldenCutscene -= FinishTutorial;
        TutorialManager.Instance.OnTutorialComplete -= FinishTutorial;
    }

    private void Start()
    {
        ball = GetComponent<BallDriving>();
    }

    private void Update()
    {
        // HOTKEY
        if(DevTools.GetKeyDown(KeyCode.S))
        {
            if(!hasLearnt)
            {
                transform.parent.GetComponentInChildren<Respawn>().StartRespawnCoroutine();
            }
            TeachHandler(TutorialType.Final);
        }
    }

    /// <summary>
    /// Resets the tutorial handler so players can be tutorialized on subsequent playthroughs.
    /// </summary>
    public void ResetHandler()
    {
        hasLearnt = false;
        if (TutorialManager.Instance.ShouldTutorialize)
        {
            ball.HardCodeBoostModifier(tutorialBoostMod);
            tutorialImage.SetActive(true);
            TeachBoosting();
        }
        else
        {
            ball.SetBoostModifier(false);
            tutorialText.text = "";
            tutorialTextPurple.text = "";
        }
    }

    /// <summary>
    /// For when the handler completes a tutorial. It'll set up the next tutorial or complete the game based on passed tutorial type.
    /// </summary>
    public void TeachHandler(TutorialType type) // add tutorial type param here
    {
        if (hasLearnt)
            return;


        // have some switch statement here to change the text based on tutorial type
        switch(type)
        {
            case (TutorialType.Boost):
                TeachBoosting();
                break;
            case (TutorialType.Pickup):
                TeachPickup();
                break;
            case(TutorialType.Dropoff): 
                TeachDropoff(); 
                break;
            case (TutorialType.Steal):
                TeachSteal();
                break;
            case (TutorialType.Final):
                FinishTutorial();
                break;
            default:
                break;
        }
    }

    private void TeachBoosting()
    {
        tutorialText.text = boostInstructions;
        tutorialTextPurple.text = boostInstructionsPurple;
    }

    private void TeachPickup()
    {
        tutorialText.text = pickupInstructions;
        tutorialTextPurple.text = pickupInstructions;
    }

    private void TeachDropoff()
    {
        tutorialText.text = dropoffInstructions;
        tutorialTextPurple.text = dropoffInstructions;
    }

    private void TeachSteal()
    {
        tutorialText.text = stealInstructions;
        tutorialTextPurple.text = stealInstructions;
    }

    public void FinishTutorial()
    {
        if (hasLearnt)
            return;

        // final tutorial
        hasLearnt = true;
        ball.SetBoostModifier(true);
        tutorialText.text = "";
        tutorialTextPurple.text = "";

        tutorialImage.SetActive(false);

        if(TutorialManager.Instance != null)
            TutorialManager.Instance.IncrementAlumni(this);
    }
}
