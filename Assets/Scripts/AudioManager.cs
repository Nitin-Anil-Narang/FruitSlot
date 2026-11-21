using UnityEngine;

/// <summary>
/// Persistent singleton that manages all slot machine sound effects:
/// mechanical lever pulls, reel spinning whirrs, reel stop latch clunks,
/// coin win chimes, jackpot celebrations, free spin bonus fanfare, and UI clicks.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Clips")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip leverPullClip;
    [SerializeField] private AudioClip reelSpinClip;
    [SerializeField] private AudioClip reelStopClip;
    [SerializeField] private AudioClip winCoinClip;
    [SerializeField] private AudioClip jackpotClip;
    [SerializeField] private AudioClip freeSpinsClip;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource spinLoopSource;

    [Header("Volume & Settings")]
    [SerializeField] private bool isMuted = false;
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    public bool IsMuted => isMuted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure 2D SFX AudioSource exists
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        // Ensure Looping Reel Spin AudioSource exists
        if (spinLoopSource == null)
        {
            spinLoopSource = gameObject.AddComponent<AudioSource>();
            spinLoopSource.playOnAwake = false;
            spinLoopSource.loop = true;
            spinLoopSource.spatialBlend = 0f;
        }

        LoadClipsIfMissing();
    }

    private void LoadClipsIfMissing()
    {
        if (buttonClickClip == null) buttonClickClip = Resources.Load<AudioClip>("Sounds/button_click");
        if (leverPullClip == null) leverPullClip = Resources.Load<AudioClip>("Sounds/lever_pull");
        if (reelSpinClip == null) reelSpinClip = Resources.Load<AudioClip>("Sounds/reel_spin");
        if (reelStopClip == null) reelStopClip = Resources.Load<AudioClip>("Sounds/reel_stop");
        if (winCoinClip == null) winCoinClip = Resources.Load<AudioClip>("Sounds/win_coin");
        if (jackpotClip == null) jackpotClip = Resources.Load<AudioClip>("Sounds/jackpot");
        if (freeSpinsClip == null) freeSpinsClip = Resources.Load<AudioClip>("Sounds/free_spins");
    }

    public void ToggleMute()
    {
        SetMute(!isMuted);
    }

    public void SetMute(bool mute)
    {
        isMuted = mute;
        if (sfxSource != null) sfxSource.mute = isMuted;
        if (spinLoopSource != null) spinLoopSource.mute = isMuted;
    }

    public void PlayButtonClick()
    {
        PlayClip(buttonClickClip, 0.6f);
    }

    public void PlayLeverPull()
    {
        PlayClip(leverPullClip, 0.9f);
    }

    public void StartReelSpin()
    {
        if (isMuted || reelSpinClip == null || spinLoopSource == null) return;
        spinLoopSource.clip = reelSpinClip;
        spinLoopSource.volume = masterVolume * 0.7f;
        if (!spinLoopSource.isPlaying)
        {
            spinLoopSource.Play();
        }
    }

    public void StopReelSpin()
    {
        if (spinLoopSource != null && spinLoopSource.isPlaying)
        {
            spinLoopSource.Stop();
        }
    }

    public void PlayReelStop(int reelIndex = 0)
    {
        if (isMuted || reelStopClip == null || sfxSource == null) return;

        // Subtle realistic mechanical pitch variation per reel (Reel 1, 2, 3)
        float pitchVariation = 0.95f + (reelIndex * 0.05f);
        sfxSource.pitch = pitchVariation;
        sfxSource.PlayOneShot(reelStopClip, masterVolume * 0.9f);
        sfxSource.pitch = 1f;
    }

    public void PlayWin(bool isJackpot = false)
    {
        if (isJackpot)
        {
            PlayJackpot();
        }
        else
        {
            PlayClip(winCoinClip, 0.85f);
        }
    }

    public void PlayJackpot()
    {
        PlayClip(jackpotClip, 1f);
    }

    public void PlayFreeSpins()
    {
        PlayClip(freeSpinsClip, 0.95f);
    }

    private void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (isMuted || clip == null || sfxSource == null) return;
        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(clip, masterVolume * volumeScale);
    }
}
