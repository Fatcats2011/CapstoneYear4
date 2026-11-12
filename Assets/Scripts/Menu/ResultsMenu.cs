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

    private ResultsVote vote; // the rematch vote, opened once the scores are up
    private ResultsAwardsView awards; // the players' cards (name, score, award, the match's numbers), shown with the scores

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

    /// <summary>The camera on the podium (tests film through it)</summary>
    internal Camera ResultsCamera => cam;

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

        if (awards == null)
            awards = ResultsAwardsView.Build(canvasElements.transform, displayText);

        int players = PlayerInstantiate.Instance.PlayerCount;
        Transform winner = players > 0 ? ScooterOf(ScoreManager.Instance.GetHandlerOfIndex(0)) : null;
        int[] seats = new int[players];
        int[] placements = new int[players];
        for (int i = 0; i < players; i++)
        {
            OrderHandler currHandler = ScoreManager.Instance.GetHandlerOfIndex(i);
            if (currHandler == null)
            {
                seats[i] = -1;
                continue;
            }

            // The pose of their place, and their acts on the podium (PodiumActs)
            Animator playerAnim = currHandler.PlayerAnimator;
            if (playerAnim != null)
                playerAnim.SetInteger(HashReference._endStatusFloat, PodiumActs.PoseOf(i));
            PodiumPerformer.On(ScooterOf(currHandler), playerAnim, i, players, winner, null);

            seats[i] = OrderSync.SeatOf(currHandler);
            placements[i] = currHandler.Placement;
            if (i < displayText.Length)
            {
                displayText[i].gameObject.SetActive(true);
                displayText[i].text = ScoreText(currHandler.Score);
                awards.ShowPlayer(i, seats[i], currHandler.Placement, seats[i] >= 0 ? Scoreboard.ShownName(seats[i]) : "", displayText[i].text);
            }
        }
        // Whoever stands alone in first wears the crown if everyone votes for a rematch
        RematchCrown.Won(RematchCrown.Winner(seats, placements));
        ShowAwards();
    }

    // A score as its card shows it: money, or in Kings Crown the time the crown was held
    private static string ScoreText(int score)
    {
        string text = Scoreboard.ScoreText(HouseRules.Playing.Mode, score);
        return text.Length > 0 ? text : "$" + score;
    }

    // The scooter's model, which the podium acts move: under the avatar the handler's Control sits in
    private static Transform ScooterOf(OrderHandler handler)
    {
        if (handler == null || handler.transform.parent == null)
            return null;
        return handler.transform.parent.Find(ScooterLook.MODEL);
    }

    // Each player's award and numbers on their card, from the awards picked here or sent by the host (MatchAwards): now,
    // or as they arrive while the scores are up
    private void ShowAwards()
    {
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
        if (awards != null)
            awards.Hide();

        // The ghosts stop their acts and their poses
        PodiumPerformer.StopAll();
        for (int i = 0; i < PlayerInstantiate.Instance.PlayerCount; i++)
        {
            OrderHandler currHandler = ScoreManager.Instance.GetHandlerOfIndex(i);
            Animator playerAnim = currHandler != null ? currHandler.PlayerAnimator : null;
            if (playerAnim != null)
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
            vote.View = ResultsVoteView.Build(canvasElements.transform, displayText, awards);

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
