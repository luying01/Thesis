using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central audio manager for PulleyMVP.
/// - Hover sound: called by QuizRaySelector when the ray enters a TouchButton.
/// - Confirm sound: called by TouchButton.TriggerButton on every press.
/// - Plays correct / wrong quiz feedback (called from QuizManager).
/// - Plays looping baseline-calibration music with fade in / fade out.
/// Audio is identical across all fidelity levels (no level-dependent logic here on purpose).
/// Attach to the AdaptiveSystem GameObject.
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("UI Sound Effects")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip confirmClip;

    [Header("Quiz Feedback")]
    [SerializeField] private AudioClip correctClip;
    [SerializeField] private AudioClip wrongClip;

    [Header("Baseline Calibration Music")]
    [SerializeField] private AudioClip baselineMusicClip;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;
    [SerializeField] private float musicFadeSeconds = 2f;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float confirmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float feedbackVolume = 0.6f;

    [Header("Behaviour")]
    [Tooltip("Minimum seconds between two hover sounds, so sweeping the ray does not spam.")]
    [SerializeField] private float hoverMinInterval = 0.08f;

    private AudioSource sfxSource;     // short one-shot sounds
    private AudioSource musicSource;   // looping baseline music
    private float lastHoverTime = -1f;
    private bool confirmPending;       // a click happened this frame
    private bool feedbackThisFrame;    // correct/wrong was played this frame
    private Coroutine musicFadeRoutine;

    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f; // 2D: same loudness regardless of head position

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;
    }

    private void LateUpdate()
    {
        // Confirm is played at the end of the frame, and skipped if a correct/wrong
        // sound was triggered by the same click (avoids two overlapping sounds).
        if (confirmPending && !feedbackThisFrame)
            PlayOneShot(confirmClip, confirmVolume);

        confirmPending = false;
        feedbackThisFrame = false;
    }

    // ---------- Public API ----------

    public void PlayHover()
    {
        if (Time.unscaledTime - lastHoverTime < hoverMinInterval) return;
        lastHoverTime = Time.unscaledTime;
        PlayOneShot(hoverClip, hoverVolume);
    }

    /// <summary>Called by TouchButton on press. Actual playback happens in LateUpdate.</summary>
    public void RequestConfirm()
    {
        confirmPending = true;
    }

    public void PlayCorrect()
    {
        feedbackThisFrame = true;
        PlayOneShot(correctClip, feedbackVolume);
    }

    public void PlayWrong()
    {
        feedbackThisFrame = true;
        PlayOneShot(wrongClip, feedbackVolume);
    }

    public void StartBaselineMusic()
    {
        if (baselineMusicClip == null)
        {
            Debug.LogWarning("[SFXManager] No baseline music clip assigned.");
            return;
        }
        if (musicSource.clip != baselineMusicClip) musicSource.clip = baselineMusicClip;
        if (!musicSource.isPlaying) musicSource.Play();
        FadeMusicTo(musicVolume, stopWhenDone: false);
    }

    public void StopBaselineMusic()
    {
        if (!musicSource.isPlaying) return;
        FadeMusicTo(0f, stopWhenDone: true);
    }

    // ---------- Internals ----------

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    private void FadeMusicTo(float target, bool stopWhenDone)
    {
        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = StartCoroutine(FadeRoutine(target, stopWhenDone));
    }

    private IEnumerator FadeRoutine(float target, bool stopWhenDone)
    {
        float start = musicSource.volume;
        float t = 0f;
        while (t < musicFadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(start, target, t / musicFadeSeconds);
            yield return null;
        }
        musicSource.volume = target;
        if (stopWhenDone) musicSource.Stop();
        musicFadeRoutine = null;
    }
}