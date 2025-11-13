// Assets/Scripts/System/GameManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    [Header("Core")]
    public CraneController crane;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text bestText;
    public TMP_Text finalScoreText;
    public GameObject startPanel;
    public GameObject gameOverPanel;

    [Header("Buttons")]
    public Button playButton;     // starts run (countdown + tutorial)
    public Button replayButton;   // replay from gameOver or pause
    public Button quitButton;

    [Header("Tutorial / Coach")]
    public TutorialCoach tutorial; // optional; assign if you want the helper overlay
    [Tooltip("Show the helper/tutorial when a run starts.")]
    public bool showTutorialOnStart = true;

    [Header("Sound Controls")]
    [Tooltip("Pause / sound Controls")]
    public Button pauseButton;
    public Button closePausePanelButton;
    public Button closeApp;
    public GameObject pausePanel;
    public Toggle bgMusic;
    public Toggle soundSFX;

    [Header("Scoring")]
    public string bestKey = "SUIKA_BEST";

    public bool IsRunning { get; private set; }
    int score;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        Application.targetFrameRate = 60;

        if (playButton) playButton.onClick.AddListener(() => StartCoroutine(StartRunRoutine()));
        if (replayButton) replayButton.onClick.AddListener(Replay);
        if (quitButton) quitButton.onClick.AddListener(QuitApp);
        if (pauseButton) pauseButton.onClick.AddListener(delegate { pausePanel.SetActive(true); Time.timeScale = 0; });
        if (closePausePanelButton) closePausePanelButton.onClick.AddListener(delegate { pausePanel.SetActive(false); Time.timeScale = 1; });
        if (closeApp) closeApp.onClick.AddListener(delegate { QuitApp(); });
        if (bgMusic) bgMusic.onValueChanged.AddListener(delegate { SoundManager.I.SetMusicEnabled(!bgMusic.isOn); });
        if (soundSFX) soundSFX.onValueChanged.AddListener(delegate { SoundManager.I.SetSfxEnabled(!soundSFX.isOn); });


        Time.timeScale = 0f; // stay paused until we actually start
    }

    void Start()
    {
        score = 0;
        if (scoreText) scoreText.text = "0";

        int best = PlayerPrefs.GetInt(bestKey, 0);
        if (bestText) bestText.text = $"Best: {best}";

        if (startPanel) startPanel.SetActive(true);
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }
    void Update()
    {
#if UNITY_WEBGL
        Screen.orientation = ScreenOrientation.Portrait;
#endif
    }
    // ----------- Public API -----------

    public void AddScore(int add)
    {
        score += Mathf.Max(0, add);
        if (scoreText) scoreText.text = score.ToString();
    }

    public void GameOver()
    {
        if (!IsRunning) return;

        IsRunning = false;
        Time.timeScale = 0f;

        // best
        int best = PlayerPrefs.GetInt(bestKey, 0);
        if (score > best) { best = score; PlayerPrefs.SetInt(bestKey, best); }
        if (bestText) bestText.text = $"Best: {best}";
        if (finalScoreText) finalScoreText.text = score.ToString();

        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    /// <summary>
    /// Replay from beginning: clears all fruits (bucket + crane),
    /// resets score, centers crane, respawns the first fruit, shows countdown, (optional) shows tutorial.
    /// </summary>
    public void Replay()
    {
        StopAllCoroutines();
        StartCoroutine(ReplayRoutine());
    }

    // ----------- Routines -----------

    IEnumerator StartRunRoutine()
    {
        // hide menus
        if (startPanel) startPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        // block gameplay input during countdown
        if (crane)
        {
            crane.SetUIBlocking(true);
            crane.ClearPressState();
            yield return StartCoroutine(crane.PlayStartCountdown(3));
            crane.ClearPressState();
            crane.SetUIBlocking(false);
        }

        // start
        IsRunning = true;
        Time.timeScale = 1f;

        // optional tutorial hint
        if (showTutorialOnStart && tutorial)
            tutorial.ShowForSeconds(2.5f); // arrows wiggle briefly
    }

    IEnumerator ReplayRoutine()
    {
        Time.timeScale = 1f;

        ClearAllFruitsInScene();

        score = 0;
        if (scoreText) scoreText.text = "0";

        if (crane)
        {
            crane.SetUIBlocking(true);
            crane.ForceClearCarriedAndCancel();
            crane.CenterCraneInstant();
            crane.SpawnNewOnCrane();
            crane.ClearPressState();
        }

        if (startPanel) startPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        if (crane)
        {
            // countdown
            yield return StartCoroutine(crane.PlayStartCountdown(3));
            crane.ClearPressState();

            // ✅ IMPORTANT: re-enable gameplay input now
            crane.SetUIBlocking(false);
        }

        IsRunning = true;
        Time.timeScale = 1f;

        if (showTutorialOnStart && tutorial)
            tutorial.ShowForSeconds(2.5f);
    }


    // ----------- Helpers -----------

    void ClearAllFruitsInScene()
    {
        // Find any active Fruit in scene (bucket or elsewhere)
        var fruits = FindObjectsOfType<Fruit>(includeInactive: false);
        foreach (var f in fruits)
        {
            // If this happens to be the one parented to the crane, Despawn will handle it cleanly
            FruitFactory.Despawn(f.gameObject);
        }
    }

    public void QuitApp()
    {
#if UNITY_WEBGL
        Time.timeScale = 0f;
        IsRunning = false;

        // Clear current scene objects
        ClearAllFruitsInScene();

        // Reset score
        score = 0;
        if (scoreText) scoreText.text = "0";

        // Hide gameplay UIs
        if (pausePanel) pausePanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        // ✅ Show start panel (for restart)
        if (startPanel) startPanel.SetActive(true);

#elif UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
