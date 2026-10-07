using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runs individual pedestrians. They move between a number of points, cycling between them.
/// </summary>
public class CivilianAgent : MonoBehaviour
{
    private const float FLING_CHANCE = 25.0f;

    [Header("Setup")]
    [Tooltip("Reference to the particle prefab")]
    [SerializeField] private GameObject flashParticles;
    [Tooltip("Reference to the renderers' parent")]
    [SerializeField] private GameObject renderBasket;
    [Tooltip("Reference to the secondary renderer parent w/ rigidbody")]
    [SerializeField] private GameObject fuckDoll;

    [Header("Values")]
    [Tooltip("How long after being hit it takes for the civilian to reappear")]
    [SerializeField] private float respawnTime = 1.0f;

    private NavMeshAgent agent;
    private float wayPointDistance = 4f;

    private Transform[] points;
    public Transform[] Points { set { points = value; } }

    private int currentIntendedPoint = 0;

    private float slowUpdateTickSpeed = 0.1f; //How frequently, in seconds, SlowUpdate runs
    private static readonly WaitForSeconds SlowUpdateWait = new WaitForSeconds(0.1f); // matches slowUpdateTickSpeed
    private IEnumerator slowUpdateCoroutine;

    private IEnumerator deathCoroutine;
    private IEnumerator kickDeathCoroutine;

    private Transform model;

    private Rigidbody fuckBody;

    private bool moving = true;
    public bool Moving => moving;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        model = transform.GetChild(0);
        fuckBody = fuckDoll.GetComponent<Rigidbody>();

        // Animators off screen don't need their transforms updated
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    /// <summary>
    /// Can be called externally to begin a pathing routine. Checks that points are loaded, then begins SlowUpdate
    /// </summary>
    public void BeginPathing()
    {
        if (points == null)
        {
            Debug.LogError(this.gameObject.name + " has no assigned patrol points.");
            return;
        }

        agent.SetDestination(points[0].position);

        Tween bob = model.DOLocalMoveY(0.3f, 0.6f, false);
        bob.SetEase(Ease.InOutSine);
        bob.SetLoops(-1, LoopType.Yoyo);

        StartSlowUpdate();
    }

    /// <summary>
    /// Runs a reduced-frequency Update.
    /// </summary>
    /// <returns>Boilerplate IEnumerator</returns>
    private IEnumerator SlowUpdate()
    {
        while (true)
        {
            if (moving && (agent.destination - agent.transform.position).magnitude <= wayPointDistance)
            {
                CyclePoints();
            }

            yield return SlowUpdateWait;
        }
    }

    /// <summary>
    /// Cycles through the set of points, resetting the count if it reaches the end
    /// </summary>
    private void CyclePoints()
    {
        currentIntendedPoint++;

        if (currentIntendedPoint >= points.Length)
            currentIntendedPoint = 0;

        agent.SetDestination(points[currentIntendedPoint].position);
    }

    /// <summary>
    /// Public method to be called when the ghost should 'die'
    /// Stops it moving, hides the model, calls the particle effect, and starts the respawn timer
    /// </summary>
    public void Die(CanKicker ck)
    {
        moving = false;

        if (Random.Range(0,100) < FLING_CHANCE)
        {
            fuckDoll.transform.position = renderBasket.transform.position;
            fuckDoll.transform.rotation = renderBasket.transform.rotation;
            fuckBody.velocity = Vector3.zero;
            fuckBody.angularVelocity = new Vector3(Random.Range(-4f,4f), Random.Range(-4f,4f),Random.Range(-4f, 4f));

            fuckDoll.SetActive(true);
            renderBasket.SetActive(false);

            ck.DoKick(fuckDoll.GetComponent<Collider>(), 1.3f, fuckDoll.transform.position - (1.5f * Vector3.up));
            StartKickDeath();
        }
        else
        {
            renderBasket.SetActive(false);
            CreateFlash(transform.position);
            StartDeath();
        }
    }

    /// <summary>
    /// Waits a duration before reappearing the ghost with another flash
    /// </summary>
    /// <returns>Boilerplate IEnumerator</returns>
    private IEnumerator Death()
    {
        yield return new WaitForSeconds(respawnTime);

        CreateFlash(transform.position);

        moving = true;
        renderBasket.SetActive(true);
    }

    private IEnumerator KickDeath()
    {
        yield return new WaitForSeconds(2 * respawnTime);

        CreateFlash(fuckBody.position);
        CreateFlash(transform.position);

        moving = true;
        renderBasket.SetActive(true);
        fuckDoll.SetActive(false);
    }

    /// <summary>
    /// Creates a flash particle then destroys it after 1.5 seconds
    /// </summary>
    /// <param name="location">The position to create the flash at</param>
    private void CreateFlash(Vector3 location)
    {
        Destroy(Instantiate(flashParticles, location, Quaternion.identity), 1.5f);
    }


    private void StartSlowUpdate()
    {
        slowUpdateCoroutine = SlowUpdate();
        StartCoroutine(slowUpdateCoroutine);
    }
    private void StartDeath()
    {
        deathCoroutine = Death();
        StartCoroutine(deathCoroutine);
    }
    private void StartKickDeath()
    {
        kickDeathCoroutine = KickDeath();
        StartCoroutine(kickDeathCoroutine);
    }}
