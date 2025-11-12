using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio; // optional (for mixer routing)

[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager I { get; private set; }

    [Header("Mixer (optional)")]
    public AudioMixer mixer;                 // optional
    public AudioMixerGroup musicGroup;       // optional
    public AudioMixerGroup sfxGroup;         // optional

    [Header("Music")]
    public AudioClip defaultMusic;           // optional: auto-play on start
    [Range(0f, 1f)] public float musicVolume = 1f;

    [Header("SFX")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("How many SFX AudioSources to pre-create (pooled).")]
    public int sfxPoolSize = 8;

    [Header("PlayerPrefs Keys")]
    public string ppMusicOnKey = "SM_MUSIC_ON";
    public string ppSfxOnKey = "SM_SFX_ON";
    public string ppMusicVolKey = "SM_MUSIC_VOL";
    public string ppSfxVolKey = "SM_SFX_VOL";

    public bool MusicEnabled { get; private set; } = true;
    public bool SfxEnabled { get; private set; } = true;

    AudioSource musicSource;
    readonly Queue<AudioSource> sfxFree = new();
    readonly HashSet<AudioSource> sfxBusy = new();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // Music source
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.outputAudioMixerGroup = musicGroup;
        musicSource.volume = musicVolume;

        // SFX pool
        for (int i = 0; i < Mathf.Max(1, sfxPoolSize); i++)
            sfxFree.Enqueue(CreateSfxSource());

        LoadPrefs();
        ApplyVolumes();

        if (defaultMusic && MusicEnabled)
            PlayMusic(defaultMusic, 0.2f, true);
    }

    AudioSource CreateSfxSource()
    {
        var go = new GameObject("[SFX]");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.outputAudioMixerGroup = sfxGroup;
        src.volume = sfxVolume;
        return src;
    }

    // ===================== PUBLIC API =====================

    // ---- MUSIC ----
    public void SetMusicEnabled(bool on)
    {
        MusicEnabled = on;
        if (!on) StopMusic(0.15f);
        else if (musicSource.clip) StartCoroutine(FadeMusicTo(musicVolume, 0.15f, playIfStopped: true));
        SavePrefs();
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        ApplyVolumes();
        SavePrefs();
    }

    public void PlayMusic(AudioClip clip, float fadeSeconds = 0.2f, bool loop = true)
    {
        if (!clip) return;
        StopAllCoroutines();
        musicSource.loop = loop;

        if (!MusicEnabled)
        {
            musicSource.clip = clip; // cache; starts when user enables
            return;
        }
        StartCoroutine(CoPlayMusic(clip, fadeSeconds));
    }

    public void StopMusic(float fadeSeconds = 0.2f)
    {
        if (musicSource.isPlaying)
            StartCoroutine(FadeMusicTo(0f, fadeSeconds, stopAfter: true));
    }

    // ---- SFX ----
    public void SetSfxEnabled(bool on)
    {
        SfxEnabled = on;
        SavePrefs();
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        ApplyVolumes();
        SavePrefs();
    }

    /// <summary>Play a one-shot SFX with optional pitch variance.</summary>
    public AudioSource PlaySFX(AudioClip clip, float volume = 1f, float pitchMin = 1f, float pitchMax = 1f)
    {
        if (!clip || !SfxEnabled) return null;

        var src = GetSfxSource();
        src.clip = clip;
        src.volume = sfxVolume * Mathf.Clamp01(volume);
        src.pitch = Mathf.Clamp(Random.Range(pitchMin, pitchMax), 0.25f, 3f);
        src.Play();
        StartCoroutine(ReturnWhenDone(src));
        return src;
    }

    public void MuteAll() { SetMusicEnabled(false); SetSfxEnabled(false); }
    public void UnmuteAll() { SetMusicEnabled(true); SetSfxEnabled(true); }

    // ===================== INTERNALS =====================

    IEnumerator CoPlayMusic(AudioClip clip, float fadeSeconds)
    {
        if (musicSource.isPlaying && musicSource.clip)
            yield return FadeMusicTo(0f, fadeSeconds);

        musicSource.clip = clip;
        musicSource.time = 0f;

        musicSource.volume = 0f;
        musicSource.Play();
        yield return FadeMusicTo(musicVolume, fadeSeconds);
    }

    IEnumerator FadeMusicTo(float target, float dur, bool stopAfter = false, bool playIfStopped = false)
    {
        if (playIfStopped && !musicSource.isPlaying) musicSource.Play();
        float start = musicSource.volume;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(start, target, dur > 0 ? t / dur : 1f);
            yield return null;
        }
        musicSource.volume = target;
        if (stopAfter) musicSource.Stop();
    }

    AudioSource GetSfxSource()
    {
        if (sfxFree.Count == 0) sfxFree.Enqueue(CreateSfxSource());
        var src = sfxFree.Dequeue();
        sfxBusy.Add(src);
        return src;
    }

    IEnumerator ReturnWhenDone(AudioSource src)
    {
        while (src && src.isPlaying) yield return null;
        if (!src) yield break;

        src.clip = null;
        src.pitch = 1f;
        src.volume = sfxVolume;
        sfxBusy.Remove(src);
        sfxFree.Enqueue(src);
    }

    void ApplyVolumes()
    {
        // If using an AudioMixer, you can expose params and map them here (log volume):
        // if (mixer) mixer.SetFloat("MusicVol", Mathf.Log10(Mathf.Max(0.0001f, musicVolume)) * 20f);
        // if (mixer) mixer.SetFloat("SfxVol",   Mathf.Log10(Mathf.Max(0.0001f, sfxVolume))   * 20f);

        if (musicSource) musicSource.volume = MusicEnabled ? musicVolume : 0f;

        foreach (var s in sfxBusy) if (s) s.volume = sfxVolume;
        foreach (var s in sfxFree) if (s) s.volume = sfxVolume;
    }

    void LoadPrefs()
    {
        MusicEnabled = PlayerPrefs.GetInt(ppMusicOnKey, 1) == 1;
        SfxEnabled = PlayerPrefs.GetInt(ppSfxOnKey, 1) == 1;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(ppMusicVolKey, 1f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(ppSfxVolKey, 1f));
    }

    void SavePrefs()
    {
        PlayerPrefs.SetInt(ppMusicOnKey, MusicEnabled ? 1 : 0);
        PlayerPrefs.SetInt(ppSfxOnKey, SfxEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(ppMusicVolKey, musicVolume);
        PlayerPrefs.SetFloat(ppSfxVolKey, sfxVolume);
        PlayerPrefs.Save();
    }
}
