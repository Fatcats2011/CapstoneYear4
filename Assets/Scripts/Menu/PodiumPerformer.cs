using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ghost putting on its podium acts (PodiumActs) at the results: it sits on the scooter's model, holds the pose of
/// its place, and every few seconds plays the next act of its mood's shuffled list. An act moves the model on top of
/// how it stood (nothing else moves the model while the scooter is frozen: BallDriving turns its parents), so stopping
/// puts it back exactly. Each machine picks its own acts: they're only for show. It stops by itself when the results
/// end, however they end (the vote, a rematch's load, the lobby): StopAll, or the game leaving the Results state
/// </summary>
public class PodiumPerformer : MonoBehaviour
{
    static readonly List<PodiumPerformer> performing = new List<PodiumPerformer>();

    Animator animator;
    PodiumAct[] acts;
    TitlePlaylist order; // a shuffled round of the acts, never the same twice running
    System.Random random;
    int pose;            // the pose of its place, held between acts
    Transform winner;    // who the looking acts look at (null: nobody, or itself)

    Vector3 restPosition;
    Quaternion restRotation;
    Vector3 restScale;

    PodiumAct act;   // the act playing (null: standing still)
    float actTime;
    float rest;      // seconds until the next act
    float toWinner;  // the winner's side as the act began, in degrees

    /// <summary>The act playing now (null between acts)</summary>
    public PodiumAct Playing => act;

    /// <summary>How many ghosts are performing</summary>
    public static int Count => performing.Count;

    /// <summary>
    /// Starts a scooter's model performing for a finishing place (0 = first) among this many players. One performer a
    /// model: asking again starts it afresh
    /// </summary>
    public static PodiumPerformer On(Transform model, Animator animator, int place, int players, Transform winner, System.Random random)
    {
        if (model == null)
            return null;
        PodiumPerformer performer = model.GetComponent<PodiumPerformer>();
        if (performer != null)
            performer.Stop();
        performer = model.gameObject.AddComponent<PodiumPerformer>();
        performer.animator = animator;
        performer.acts = PodiumActs.For(PodiumActs.MoodOf(place, players));
        performer.random = random ?? new System.Random();
        performer.order = new TitlePlaylist(performer.acts.Length, performer.random);
        performer.pose = PodiumActs.PoseOf(place);
        performer.winner = winner != model ? winner : null;
        performer.restPosition = model.localPosition;
        performer.restRotation = model.localRotation;
        performer.restScale = model.localScale;
        // Not all at once: each ghost starts in its own time
        performer.rest = 0.4f + (float)performer.random.NextDouble() * 2f;
        performer.SetPose(performer.pose);
        performing.Add(performer);
        return performer;
    }

    /// <summary>Every ghost stops and stands as it did (the results are over)</summary>
    public static void StopAll()
    {
        for (int i = performing.Count - 1; i >= 0; i--)
        {
            if (performing[i] != null)
                performing[i].Stop();
        }
        performing.Clear();
    }

    void Update()
    {
        // The results ended some way that told nobody here (a host's load, a player leaving for the lobby)
        if (GameManager.Instance != null && GameManager.Instance.MainState != GameState.Results)
        {
            Stop();
            return;
        }
        Tick(Time.deltaTime);
    }

    /// <summary>That much time goes by: the act moves on, or the wait for the next one does</summary>
    internal void Tick(float deltaTime)
    {
        if (act == null)
        {
            rest -= deltaTime;
            if (rest <= 0f)
                Begin(acts[order.Next()]);
            return;
        }

        actTime += deltaTime;
        if (actTime >= act.Length)
        {
            End();
            return;
        }
        Apply(act.At(actTime / act.Length, toWinner));
    }

    /// <summary>Starts an act now, whatever was playing (tests, and Tick)</summary>
    internal void Begin(PodiumAct next)
    {
        act = next;
        actTime = 0f;
        toWinner = WinnerSide();
        if (act.Pose != PodiumAct.KEEP)
            SetPose(act.Pose);
        Apply(act.At(0f, toWinner));
    }

    void End()
    {
        if (act != null && act.Pose != PodiumAct.KEEP)
            SetPose(pose);
        act = null;
        Apply(PodiumPose.Still);
        rest = PodiumActs.REST_MIN + (float)random.NextDouble() * (PodiumActs.REST_MAX - PodiumActs.REST_MIN);
    }

    /// <summary>Stops performing: the model stands as it did, and the performer goes. The pose is the results' to clear</summary>
    public void Stop()
    {
        act = null;
        Apply(PodiumPose.Still);
        performing.Remove(this);
        enabled = false;
        if (Application.isPlaying)
            Destroy(this);
        else
            DestroyImmediate(this);
    }

    void OnDestroy()
    {
        performing.Remove(this);
    }

    // The scooter's nose is its model's -x, its right +z (BallDriving pushes it along -right), up +y
    void Apply(PodiumPose at)
    {
        transform.localPosition = restPosition + restRotation * (Vector3.up * at.Lift);
        transform.localRotation = restRotation * Quaternion.Euler(-at.Lean, at.Yaw, -at.Nose);
        transform.localScale = Vector3.Scale(restScale, new Vector3(at.Wide, at.Tall, at.Wide));
    }

    // How far to its right the winner stands, in degrees (0: nobody to look at)
    float WinnerSide()
    {
        if (winner == null)
            return 0f;
        Transform parent = transform.parent;
        Quaternion standing = parent != null ? parent.rotation * restRotation : restRotation;
        Vector3 nose = standing * Vector3.left;
        Vector3 to = winner.position - transform.position;
        nose.y = to.y = 0f;
        if (nose.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f)
            return 0f;
        return Vector3.SignedAngle(nose, to, Vector3.up);
    }

    void SetPose(int endStatus)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetInteger(HashReference._endStatusFloat, endStatus);
    }
}
