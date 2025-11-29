using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SoundManager singleton
/// - Background music playlist support (select / next / prev / random)
/// - SFX playback (PlaySfx enum + PlayOneShot)
/// - Persist music & sfx toggles and selected music index in PlayerPrefs
/// </summary>
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager I { get; private set; }

    // PlayerPrefs keys
    const string MUSIC_KEY = "SUIKA_MUSIC_ON";
    const string SFX_KEY = "SUIKA_SFX_ON";
    const string MUSIC_INDEX_KEY = "SUIKA_MUSIC_INDEX";

    [Header("Sources (optional assign)")]
    public AudioSource musicSource;     // background music (looping)
    public AudioSource sfxSource;       // sfx source (PlayOneShot)

    [Header("Music Playlist (assign multiple clips)")]
    [Tooltip("List of background music tracks. Use index to select which track to play.")]
    public List<AudioClip> musicTracks = new List<AudioClip>();

    [Header("SFX Clips (assign in inspector)")]
    public AudioClip dropClip;          // played when crane drops fruit
    public AudioClip mergeClip;         // played on successful merge
    public AudioClip spawnClip;         // optional: when fruit spawns on crane
    public AudioClip popClip;           // optional: small pop or level-up

    [Header("Volumes")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    // runtime state
    bool musicEnabled = true;
    bool sfxEnabled = true;

    // current music index in playlist
    int currentMusicIndex = 0;

    // Optional: small pitch variance for SFX
    public bool randomizeSfxPitch = false;
    [Range(0.0f, 0.2f)] public float sfxPitchVariance = 0.06f;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // ensure AudioSources exist
        if (musicSource == null)
        {
            var go = new GameObject("MusicSource");
            go.transform.SetParent(transform);
            musicSource = go.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            var go = new GameObject("SfxSource");
            go.transform.SetParent(transform);
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        // load prefs
        musicEnabled = PlayerPrefs.GetInt(MUSIC_KEY, 1) == 1;
        sfxEnabled = PlayerPrefs.GetInt(SFX_KEY, 1) == 1;
        currentMusicIndex = PlayerPrefs.GetInt(MUSIC_INDEX_KEY, 0);

        // clamp current index
        if (musicTracks == null) musicTracks = new List<AudioClip>();
        if (musicTracks.Count == 0) currentMusicIndex = 0;
        else currentMusicIndex = Mathf.Clamp(currentMusicIndex, 0, musicTracks.Count - 1);

        ApplyVolumes();

        // Auto-start the saved music track (only if a track exists)
        if (musicTracks.Count > 0)
        {
            // Play saved track on awake only if music enabled.
            // On WebGL/music, browsers often require a user interaction to start audio.
            if (musicEnabled)
                PlayCurrentMusic();
        }
    }

    void ApplyVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicEnabled ? musicVolume : 0f;
        }
        if (sfxSource != null)
        {
            sfxSource.volume = sfxEnabled ? sfxVolume : 0f;
        }
    }

    #region Public API - Toggles / Query

    public void SetMusicEnabled(bool enabled)
    {
        musicEnabled = enabled;
        PlayerPrefs.SetInt(MUSIC_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolumes();

        if (musicEnabled)
        {
            // If enabled and we have a clip, ensure it's playing
            if (musicTracks != null && musicTracks.Count > 0)
                PlayCurrentMusic();
        }
        else
        {
            if (musicSource != null) musicSource.Pause();
        }
    }

    public void SetSfxEnabled(bool enabled)
    {
        sfxEnabled = enabled;
        PlayerPrefs.SetInt(SFX_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    public bool IsMusicEnabled() => musicEnabled;
    public bool IsSfxEnabled() => sfxEnabled;

    #endregion

    #region Music Playlist Controls

    /// <summary>
    /// Set the current music track by index and start playing it (looped).
    /// Index is clamped. Saves selection to PlayerPrefs.
    /// </summary>
    public void SetMusicTrack(int index)
    {
        if (musicTracks == null || musicTracks.Count == 0) return;
        currentMusicIndex = Mathf.Clamp(index, 0, musicTracks.Count - 1);
        PlayerPrefs.SetInt(MUSIC_INDEX_KEY, currentMusicIndex);
        PlayerPrefs.Save();
        PlayCurrentMusic();
    }

    public int GetCurrentTrackIndex() => currentMusicIndex;
    public int GetMusicTrackCount() => musicTracks?.Count ?? 0;
    public AudioClip GetCurrentTrackClip() => (musicTracks != null && musicTracks.Count > 0) ? musicTracks[currentMusicIndex] : null;

    /// <summary>
    /// Start playing the current music (looping).
    /// </summary>
    public void PlayCurrentMusic()
    {
        if (musicSource == null) return;
        if (musicTracks == null || musicTracks.Count == 0)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        var clip = musicTracks[currentMusicIndex];
        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = musicEnabled ? musicVolume : 0f;
        musicSource.Play();
    }

    /// <summary>
    /// Play next track in playlist (wraps around).
    /// </summary>
    public void PlayNextTrack()
    {
        if (musicTracks == null || musicTracks.Count == 0) return;
        currentMusicIndex = (currentMusicIndex + 1) % musicTracks.Count;
        PlayerPrefs.SetInt(MUSIC_INDEX_KEY, currentMusicIndex);
        PlayerPrefs.Save();
        PlayCurrentMusic();
    }

    /// <summary>
    /// Play previous track in playlist (wraps around).
    /// </summary>
    public void PlayPrevTrack()
    {
        if (musicTracks == null || musicTracks.Count == 0) return;
        currentMusicIndex = (currentMusicIndex - 1 + musicTracks.Count) % musicTracks.Count;
        PlayerPrefs.SetInt(MUSIC_INDEX_KEY, currentMusicIndex);
        PlayerPrefs.Save();
        PlayCurrentMusic();
    }

    /// <summary>
    /// Play a random track from the playlist.
    /// </summary>
    public void PlayRandomTrack()
    {
        if (musicTracks == null || musicTracks.Count == 0) return;
        int next = UnityEngine.Random.Range(0, musicTracks.Count);
        currentMusicIndex = next;
        PlayerPrefs.SetInt(MUSIC_INDEX_KEY, currentMusicIndex);
        PlayerPrefs.Save();
        PlayCurrentMusic();
    }

    /// <summary>
    /// Stop music playback
    /// </summary>
    public void StopMusic()
    {
        if (musicSource == null) return;
        musicSource.Stop();
    }

    /// <summary>
    /// Preview a music clip once (non-looping) — useful for UI preview buttons.
    /// </summary>
    public void PreviewMusic(int index)
    {
        if (musicTracks == null || musicTracks.Count == 0 || sfxSource == null) return;
        index = Mathf.Clamp(index, 0, musicTracks.Count - 1);
        var clip = musicTracks[index];
        if (clip == null) return;
        // Use sfxSource to preview so it doesn't alter the main musicSource clip
        sfxSource.PlayOneShot(clip, Mathf.Min(1f, musicVolume));
    }

    #endregion

    #region SFX API

    /// <summary>
    /// Play one of the predefined SFX by enum
    /// </summary>
    public enum SfxType { Drop, Merge, Spawn, Pop }

    public void PlaySfx(SfxType type)
    {
        if (!sfxEnabled || sfxSource == null) return;

        AudioClip clip = null;
        switch (type)
        {
            case SfxType.Drop: clip = dropClip; break;
            case SfxType.Merge: clip = mergeClip; break;
            case SfxType.Spawn: clip = spawnClip; break;
            case SfxType.Pop: clip = popClip; break;
        }

        if (clip == null) return;

        if (randomizeSfxPitch)
        {
            float basePitch = sfxSource.pitch;
            sfxSource.pitch = UnityEngine.Random.Range(1f - sfxPitchVariance, 1f + sfxPitchVariance);
            sfxSource.PlayOneShot(clip, sfxVolume);
            sfxSource.pitch = basePitch;
        }
        else
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (!sfxEnabled || sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    #endregion

    #region Editor helpers (context menu for quick tests)

    [ContextMenu("Play Next Music (Editor)")]
    void Context_PlayNext() { PlayNextTrack(); }

    [ContextMenu("Play Prev Music (Editor)")]
    void Context_PlayPrev() { PlayPrevTrack(); }

    [ContextMenu("Play Random Music (Editor)")]
    void Context_PlayRandom() { PlayRandomTrack(); }

    #endregion
}
