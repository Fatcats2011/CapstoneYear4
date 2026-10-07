using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class checks when the player has started their phase. Could be useful for playing a sound effect when they enter a building or showing some visual effect.
/// </summary>
public class PhaseChecker : MonoBehaviour
{
    [Header("Some position representing the physical ghost. Could be the mesh, ball, etc...")]
    [SerializeField] private Transform target;
    private BallDriving driving;
    private SoundPool soundPool;

    private void Start()
    {
        driving = GetComponentInParent<BallDriving>();
        soundPool = GetComponentInParent<SoundPool>();
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, target.position - transform.position, out hit, Vector3.Distance(transform.position, target.position), 1 << 9))
        {
            if(hit.collider != target.gameObject && driving.Boosting) // hit a wall and player is boosting (aka they began to phase)
            {
                soundPool.PlayPhaseSound(); // we could maybe change this to an event if we have a bunch of scripts that need to check when phase starts, we'll need some dirtyPhase bool or something though
            }
        }
    }
}
