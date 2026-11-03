using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResultsMenu : SingletonMonobehaviour<ResultsMenu>
{
    [SerializeField] internal TMP_Text[] displayText;
    [SerializeField] Canvas resultsCanvas;

    [SerializeField] private GameObject quitInfo;

    [SerializeField] private Camera cam;
    internal bool canQuit = false;

    [SerializeField] private GameObject wiper;
    [SerializeField] private GameObject canvasElements;
    private Animator transitionAnimator, camAnimator;

    [Tooltip("Basically the length of the camera animation.")]
    [SerializeField] private float timeBeforeUI = 4f;

    private IEnumerator resultsRoutine;

    /// <summary>How tall each player's name over their score is, in the score's own units (the award cards sit above it)</summary>
    internal const float NAME_LINE = 30f;

    private ResultsVote vote; // the rematch vote, opened once the scores are up
    private TMP_Text[] names; // each player's name over their score
    private ResultsAwardsView awards; // the award cards, shown with the scores

    private void OnEnable()
    {
        GameManager.Instance.OnSwapResults += InitResults;
        GameManager.Instance.OnFinalOrderDelivered += HideGame;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapResults -= InitResults;
        GameManager.Instance.OnFinalOrderDelivered -= HideGame;
    }

    /// <summary>Whether the results camera shows (tests)</summary>
    internal bool ResultsCameraOn => cam != null && cam.enabled;

    /// <summary>Whether the scores are up (tests)</summary>
    internal bool ScoresShown => canvasElements != null && canvasElements.activeSelf && resultsCanvas.enabled;

    // In Awake, not Start: with no golden round the results can begin the moment this scene does, before its Starts
    // run (and an online client's queued Results state is applied as the scene loads)
    protected override void Awake()
    {
        base.Awake();
        cam.enabled = false;
        transitionAnimator = wiper.GetComponent<Animator>();
        camAnimator = cam.GetComponent<Animator>();
        vote = GetComponent<ResultsVote>();
        if (vote == null)
            vote = gameObject.AddComponent<ResultsVote>();
    }

    private void HideGame()
    {
        canvasElements.SetActive(false);
        resultsCanvas.enabled = true;
        wiper.SetActive(true);
        transitionAnimator.SetTrigger(HashReference._wipeInTrigger);
    }

    private void InitResults()
    {
        if (resultsRoutine != null)
        {
            StopCoroutine(resultsRoutine);
            resultsRoutine = null;
        }
        resultsRoutine = UpdateResults();
        StartCoroutine(resultsRoutine);
    }

    private IEnumerator UpdateResults()
    {
        transitionAnimator.SetTrigger(HashReference._wipeOutTrigger);
        cam.enabled = true;
        camAnimator.SetTrigger(HashReference._startTrigger);
        quitInfo.SetActive(false);

        yield return new WaitForSeconds(timeBeforeUI);
        
        canvasElements.SetActive(true);
        resultsCanvas.enabled = true;
        StartCoroutine(QuitDelay());

        for (int i = 0; i < PlayerInstantiate.Instance.PlayerCount; i++)
        {
            OrderHandler currHandler = ScoreManager.Instance.GetHandlerOfIndex(i);

            // Set player animations
            Animator playerAnim = currHandler.PlayerAnimator;
            playerAnim.SetInteger(HashReference._endStatusFloat, i + 1);

            if (currHandler != null)
            {
                displayText[i].gameObject.SetActive(true);
                displayText[i].text = "$" + currHandler.Score;
                ShowName(i, OrderSync.SeatOf(currHandler));
            }
        }
        ShowAwards();
    }

    // Each player's name just over their score, as the scoreboard showed it (playtest, 2026-11-03: the podium had none)
    private void ShowName(int score, int seat)
    {
        if (names == null)
            names = new TMP_Text[displayText.Length];
        if (names[score] == null)
            names[score] = NameLabel(displayText[score]);
        names[score].text = seat >= 0 ? Scoreboard.ShownName(seat) : "";
        names[score].enabled = true;
    }

    // A label on top of a score's text box, inside its margins, aligned like it and in its font and colour
    private static TMP_Text NameLabel(TMP_Text score)
    {
        TextMeshProUGUI label = new GameObject("Player Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        RectTransform rect = label.rectTransform;
        rect.SetParent(score.transform, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(score.margin.x, score.margin.y);
        rect.offsetMax = new Vector2(-score.margin.z, score.margin.y + NAME_LINE);
        if (score.font != null)
            label.font = score.font;
        label.color = score.color;
        label.horizontalAlignment = score.horizontalAlignment;
        label.verticalAlignment = VerticalAlignmentOptions.Bottom;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 24f;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    // Each player's award card over their score, from the awards picked here or sent by the host (MatchAwards): now, or
    // as they arrive while the scores are up
    private void ShowAwards()
    {
        if (awards == null)
            awards = ResultsAwardsView.Build(canvasElements.transform, displayText);
        awards.Follow(SeatOfScores());
    }

    // The seat each score shows: the scores are in finishing order (UpdateResults); -1 for a score nobody has
    private int[] SeatOfScores()
    {
        int[] seatOfScore = new int[displayText.Length];
        for (int i = 0; i < seatOfScore.Length; i++)
            seatOfScore[i] = i < PlayerInstantiate.Instance.PlayerCount ? OrderSync.SeatOf(ScoreManager.Instance.GetHandlerOfIndex(i)) : -1;
        return seatOfScore;
    }

    public void ConfirmMenu()
    {
        // Online, the host takes everyone back from the results; a client waits for it
        if (!canQuit || !GameAuthority.IsAuthority)
        {
            return;
        }
        SceneFlow.Current.ReturnToMenu();
        ClearResultsView();
    }

    ///<summary>
    /// The results are done with (the vote's outcome, or the return): the scores hide and the scooters stop their
    /// finishing animations
    ///</summary>
    internal void ClearResultsView()
    {
        for (int i = 0; i < displayText.Length; i++)
        {
            displayText[i].enabled = false;
        }
        if (names != null)
        {
            foreach (TMP_Text name in names)
            {
                if (name != null)
                    name.enabled = false;
            }
        }
        if (awards != null)
            awards.Hide();

        // Reset player result animations
        for (int i = 0; i < PlayerInstantiate.Instance.PlayerCount; i++)
        {
            OrderHandler currHandler = ScoreManager.Instance.GetHandlerOfIndex(i);
            Animator playerAnim = currHandler.PlayerAnimator;
            playerAnim.SetInteger(HashReference._endStatusFloat, 0);
        }
    }

    private IEnumerator QuitDelay()
    {
        canQuit = false;
        yield return new WaitForSeconds(2f);
        canQuit = true;
        OpenVote();
    }

    // Every player votes on a rematch: the old "press A to quit" stays hidden
    private void OpenVote()
    {
        if (vote == null)
            return;
        if (vote.View == null)
            vote.View = ResultsVoteView.Build(canvasElements.transform, displayText);

        // The scores are in finishing order (UpdateResults): each mark goes under its own player's
        vote.View.ShowSeats(SeatOfScores());

        List<int> seats = new List<int>();
        int hostSeat = -1;
        foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
        {
            seats.Add(player.Index);
            // Online, the host's own player: its leaving (or running out of time) takes everyone back
            if (GameAuthority.IsOnline && hostSeat < 0 && player.IsLocal && GameAuthority.IsAuthority)
                hostSeat = player.Index;
        }
        vote.Open(seats, hostSeat, GameAuthority.IsAuthority);

        // A CPU driver always wants another round
        if (GameAuthority.IsAuthority)
        {
            foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
            {
                if (player.IsBot)
                    vote.Press(player.Index, true);
            }
        }
    }
}
