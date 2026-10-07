using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundPool : MonoBehaviour
{
    private AudioSource[] sourcePool;

    // refs to specific sources
    private AudioSource drivingSource;
    private AudioSource engineSource;
    private AudioSource brakeSource;
    private AudioSource driftSource;
    private AudioSource boostSource;
    private AudioSource miniBoostSource;
    private AudioSource driftSparkSource;

    private int currDriftIndex = -1;

    // bools for controlling when certain sounds should play
    private bool shouldPlay = false;
    private bool phasing = false;

    // another machine's scooter (online): the phasing sound it plays for its owner, until their phase ends
    private AudioSource remotePhaseSource;

    private void Awake()
    {
        GameObject sourceGO;
        sourcePool = new AudioSource[SoundManager.Instance.PoolSize];
        GameObject audioSource = SoundManager.Instance.AudioSourcePrefab;
        for(int i=0;i<sourcePool.Length; i++)
        {
            sourceGO = Instantiate(audioSource, transform);
            sourcePool[i] = sourceGO.GetComponent<AudioSource>();
            sourcePool[i].gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        GameManager.Instance.OnSwapTutorial += InitEngineSource;
        GameManager.Instance.OnSwapFinalPackage += InitEngineSource;
        GameManager.Instance.OnSwapResults += TurnOffPlayerSounds;
        GameManager.Instance.OnSwapMenu += TurnOffPlayerSounds;
        GameManager.Instance.OnSwapAnything += StopAudio;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapTutorial -= InitEngineSource;
        GameManager.Instance.OnSwapFinalPackage -= InitEngineSource;
        GameManager.Instance.OnSwapResults -= TurnOffPlayerSounds;
        GameManager.Instance.OnSwapMenu -= TurnOffPlayerSounds;
        GameManager.Instance.OnSwapAnything -= StopAudio;
    }

    /// <summary>
    /// This method resets an audio source so it is able to be pulled from the sound pool again.
    /// </summary>
    /// <param name="source">AudioSource to be reset</param>
    private void ResetSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.gameObject.SetActive(false);
        SoundManager.Instance.SwitchSource(ref source, "SFX");
        source.volume = 1;
        source.pitch = 1;
        source.loop = false;
    }

    /// <summary>
    /// Gets an availabe audio source from the pool. Creates a new one if none are available.
    /// </summary>
    /// <returns></returns>
    public AudioSource GetAvailableSource()
    {
        foreach(AudioSource source in sourcePool)
        {
            if(!source.gameObject.activeInHierarchy)
            {
                return source;
            }
        }
        // hopefully won't get here, if it happens frequently we can increase the pool size in SM
        return Instantiate(SoundManager.Instance.AudioSourcePrefab, transform).GetComponent<AudioSource>();
    }

    private void InitEngineSource()
    {
        if(engineSource == null) { engineSource = GetAvailableSource(); }
        SoundManager.Instance.SwitchSource(ref engineSource, "Player");
        engineSource.loop = true;
        shouldPlay = true;
        PlayIdleSound();
    }

    private void TurnOffPlayerSounds()
    {
        shouldPlay = false;
        if (engineSource == null) { return; }
        ResetSource(engineSource);
        engineSource = null;
    }

    // below are methods for starting and stopping specific sounds. Because of this there's no need to use our normal commenting standards.

    public void PlayDrivingSound()
    {
        if (!shouldPlay || drivingSource != null) { return; };
        drivingSource = GetAvailableSource();
        SoundManager.Instance.SwitchSource(ref drivingSource, "Player");
        drivingSource.loop = true;
        SoundManager.Instance.PlayEngineSound(drivingSource);
    }
    public void StopDrivingSound()
    {
        if(drivingSource != null)
        {
            ResetSource(drivingSource);
            drivingSource = null;
        }
    }
    public void PlayIdleSound()
    {
        if(!shouldPlay) { return; };
        if(drivingSource != null)
        {
            drivingSource.Stop();
            ResetSource(drivingSource);
            drivingSource = null;
        }
        engineSource.volume = 0.5f;
        engineSource.loop = true;
        SoundManager.Instance.PlayIdleSound(engineSource);
    }
    public void PlayDriftSound()
    {
        return; // TODO: fix this or don't play a drift
        if(driftSource != null) { return; }
        driftSource = GetAvailableSource();
        SoundManager.Instance.SwitchSource(ref driftSource, "Player");
        driftSource.loop = true;
        SoundManager.Instance.PlaySFX("drift", driftSource);

    }
    public void StopDriftSound()
    {
        return; // yeah we're not playing a drift sound
        if(driftSource == null) { return; }
        ResetSource(driftSource);
        driftSource = null;
    }
    public void PlayBrakeSound()
    {
        if(brakeSource == null) { brakeSource = GetAvailableSource(); }
        if (!shouldPlay || brakeSource.isPlaying) { return; }
        SoundManager.Instance.SwitchSource(ref brakeSource, "Player");
        SoundManager.Instance.PlaySFX("brake", brakeSource);
        StartCoroutine(KillSource(brakeSource));
    }
    public void PlayBoostReady()
    {
        if (!shouldPlay)
            return;

        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX("boost_charged", source);
        StartCoroutine(KillSource(source));
        Cue(CueKind.HornReady);
    }
    public void PlayBoostActivate()
    {
        if (!shouldPlay)
            return;

        boostSource = GetAvailableSource();
        SoundManager.Instance.SwitchSource(ref boostSource, "Player");
        SoundManager.Instance.PlaySFX("boost_used", boostSource);
        StartCoroutine(KillSource(boostSource));
        Cue(CueKind.Boost);
    }
    public void PlayMiniBoost()
    {
        if (!shouldPlay) 
        { 
            return; 
        }

        if (miniBoostSource == null) 
        { 
            miniBoostSource = GetAvailableSource(); 
        }
        
        miniBoostSource.loop = false;
        SoundManager.Instance.SwitchSource(ref miniBoostSource, "Player");
        SoundManager.Instance.PlaySFX("mini", miniBoostSource);
        StartCoroutine(KillSource(miniBoostSource));
        Cue(CueKind.DriftBoost);
    }
    public void PlayPhaseSound()
    {
        if(phasing || boostSource == null || !shouldPlay) { return; }
        SoundManager.Instance.PlaySFX("phasing", boostSource);
        phasing = true;
        Cue(CueKind.Phase);
    }
    public void StopPhaseSound()
    {
        if(engineSource == null || boostSource == null) { return; }
        bool wasPhasing = phasing;
        ResetSource(boostSource);
        boostSource = null;
        phasing = false;
        engineSource.Play();
        if (wasPhasing)
            Cue(CueKind.PhaseEnd);
    }
    // the order sounds play on every machine: each replays the host's order changes, on every scooter
    public void PlayOrderPickup()
    {
        if (RemoteAvatar.IsRemote(this))
        {
            PlayAtDistance("pickup");
            return;
        }
        if (!shouldPlay)
            return;

        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX("pickup", source);
        StartCoroutine(KillSource(source));
    }
    public void PlayOrderDropoff(string dropoffType = "dropoff")
    {
        if (RemoteAvatar.IsRemote(this))
        {
            PlayAtDistance(dropoffType);
            return;
        }
        if(!shouldPlay)
            return;

        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX(dropoffType, source);
        StartCoroutine(KillSource(source));
    }
    public void PlayOrderTheft()
    {
        if (RemoteAvatar.IsRemote(this))
        {
            PlayAtDistance("whoosh");
            return;
        }
        if (!shouldPlay)
            return;

        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX("whoosh", source);
        StartCoroutine(KillSource(source));
    }
    public void PlayDeathSound()
    {
        if (!shouldPlay)
            return;

        AudioSource source = GetAvailableSource();
        SoundManager.Instance.SwitchSource(ref source, "Player");
        SoundManager.Instance.PlaySFX("death", source);
        StartCoroutine(KillSource(source));
        Cue(CueKind.Death);
    }

    /// <summary>
    /// Another machine's scooter (online; this pool is off there): plays one of its owner's one-shots here, quieter the
    /// farther it is from this machine's player (RemoteSound). A phase's end stops the phasing sound it still plays
    /// </summary>
    public void PlayRemote(CueKind kind)
    {
        if (kind == CueKind.PhaseEnd)
        {
            // the source may have ended its sound, and been taken for another, since
            if (remotePhaseSource != null && remotePhaseSource.isPlaying && remotePhaseSource.clip == SoundManager.Instance.GetSFX("phasing").clip)
                ResetSource(remotePhaseSource);
            remotePhaseSource = null;
            return;
        }

        string key = RemoteSound.KeyFor(kind);
        if (key == null)
            return;

        AudioSource source = PlayAtDistance(key);
        if (kind == CueKind.Phase)
            remotePhaseSource = source;
    }

    // Another machine's scooter: a one-shot, as loud as its distance from this machine's player allows. Out of earshot it
    // takes no source and plays nothing
    private AudioSource PlayAtDistance(string key)
    {
        float volume = RemoteSound.VolumeAt(transform.position);
        if (volume <= 0f)
            return null;

        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX(key, source);
        source.volume *= volume;
        StartCoroutine(KillSource(source));
        return source;
    }

    // Online: a one-shot of this machine's player goes to the other machines, which play it on their scooter there
    // (OnlineCues). Order sounds don't: every machine replays the host's order changes
    private void Cue(CueKind kind)
    {
        if (GameAuthority.IsOnline && !RemoteAvatar.IsRemote(this))
            CueSync.Play(OrderSync.SeatOf(this), ScooterCue.Of(kind));
    }

    // UI sounds
    public void PlayEnterUI()
    {
        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX("confirm", source);
        StartCoroutine(KillSource(source));
    }
    public void PlayBackUI()
    {
        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX("back", source);
        StartCoroutine(KillSource(source));
    }
    public void PlayScrollUI()
    {
        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX("scroll", source);
        StartCoroutine(KillSource(source));
    }
    public void PlayPauseUI()
    {
        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlaySFX("pause", source);
        StartCoroutine(KillSource(source));
    }

    public void PlayDriftSpark(int index)
    {
        if (currDriftIndex == index || !shouldPlay) { return; }
        currDriftIndex = index;
        if(driftSparkSource == null)
            driftSparkSource = GetAvailableSource();

        SoundManager.Instance.SwitchSource(ref driftSparkSource, "SFX");
        SoundManager.Instance.PlayDriftSparkSound(driftSparkSource, index);
    }

    public void StopDriftSpark()
    {
        if (driftSparkSource != null)
        {
            ResetSource(driftSparkSource);
            driftSparkSource = null;
        }
        currDriftIndex = -1;
    }    

    // emote
    public void PlayEmote(int index)
    {
        AudioSource source = GetAvailableSource();
        SoundManager.Instance.PlayEmoteSound(source, index);
        StartCoroutine (KillSource(source));
    }

    public void StopAudio()
    {
        foreach(AudioSource source in sourcePool)
        {
            source.Stop();
        }
    }

    /// <summary>
    /// This coroutine is used for killing non-looping SFXs. It also won't be called with dedicated methods.
    /// </summary>
    /// <param name="source">Source to be killed after playback</param>
    /// <returns></returns>
    private IEnumerator KillSource(AudioSource source)
    {
        while(source.isPlaying)
        {
            yield return null;
        }
        ResetSource(source);
    }
}
