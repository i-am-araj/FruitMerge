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
    public Transform bucketTransform;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text bestText;
    public TMP_Text finalScoreText;
    public GameObject startPanel;
    public GameObject gameOverPanel;

    [Header("Buttons")]
    public Button playButton;
    public Button replayButton;
    public Button quitButton;
    [SerializeField] TMP_Text countdownText;   // start-only countdown text (Raycast Target OFF)

    [Header("Tutorial / Coach")]
    public TutorialManager tutorial;
    public bool showTutorialOnStart = true;

    [Header("Pause / Sound")]
    public Button pauseButton;
    public Button closePausePanelButton;
    public Button closeApp;
    public GameObject pausePanel;
    public Toggle bgMusic;
    public Toggle soundSFX;

    [Header("Scoring")]
    public string bestKey = "SUIKA_BEST";

    [Header("Ads")]
    public int scoreAdInterval = 200;   // midgame ad every 200 points
    private int nextScoreAd = 0;
    private bool midgameAdRunning = false;

    public bool IsRunning { get; private set; }
    int score;
    [SerializeField] bool directStart=false;

    // --------------------------------------------------------------------
    // INITIALIZATION
    // --------------------------------------------------------------------
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        Application.targetFrameRate = 60;

        // Button hooks
        if (directStart)
        {
            if (startPanel) startPanel.SetActive(false);
            StartCoroutine(StartRunRoutine());
        }
        else
        {
            if (startPanel) startPanel.SetActive(true);
        }
        if (playButton) playButton.onClick.AddListener(() => StartCoroutine(StartRunRoutine_WithAd()));
        if (replayButton) replayButton.onClick.AddListener(() => StartCoroutine(ReplayRoutine_WithAd()));
        if (quitButton) quitButton.onClick.AddListener(QuitApp);
        if (pauseButton) pauseButton.onClick.AddListener(() =>
        {
            pausePanel.SetActive(true);
            Time.timeScale = 0;

            //if (showBannerInPause)
            //{
            //    if (AdManager.I != null) AdManager.I.ShowBanner();
            //}
        });

        if (closePausePanelButton) closePausePanelButton.onClick.AddListener(() =>
        {
            pausePanel.SetActive(false);
            Time.timeScale = 1;
            //if (AdManager.I != null) AdManager.I.HideBanner();
        });

        if (closeApp) closeApp.onClick.AddListener(() => QuitApp());

        if (bgMusic) bgMusic.onValueChanged.AddListener((_) =>
        {
            if (SoundManager.I != null) SoundManager.I.SetMusicEnabled(!bgMusic.isOn);
        });
        if (soundSFX) soundSFX.onValueChanged.AddListener((_) =>
        {
            if (SoundManager.I != null) SoundManager.I.SetSfxEnabled(!soundSFX.isOn);
        });

        Time.timeScale = 0f;
    }

    void Start()
    {
        score = 0;
        if (scoreText) scoreText.text = "0";
        int best = 0;
        if (DataManager.instance != null) best = DataManager.instance.GetInt(bestKey, 0);

        if (bestText) bestText.text = $"Best: {best}";

        
        if (gameOverPanel) gameOverPanel.SetActive(false);

        nextScoreAd = scoreAdInterval;
        if (countdownText) countdownText.gameObject.SetActive(false);
        //if (showBannerInMenu && AdManager.I != null)
        //    AdManager.I.ShowBanner();
    }

#if UNITY_WEBGL
    void Update()
    {
        Screen.orientation = ScreenOrientation.Portrait;
    }
#endif
    // ----- Start-only countdown (called by GameManager before unpausing) -----
    public IEnumerator PlayStartCountdown(int seconds)
    {
        if (!countdownText)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield break;
        }

        countdownText.gameObject.SetActive(true);
        var rt = countdownText.rectTransform;

        for (int t = seconds; t >= 1; t--)
        {
            countdownText.text = t.ToString();
            yield return Pop(rt, 0.55f, 0.6f, 1.15f, 1.0f);
            yield return new WaitForSecondsRealtime(0.05f);
        }

        countdownText.text = "GO!";
        yield return Pop(rt, 0.45f, 0.6f, 1.2f, 1.0f);

        countdownText.gameObject.SetActive(false);
        GameManager.I.tutorial.ShowOnceForFirstRun();

    }

    // --------------------------------------------------------------------
    // PLAY (WITH REWARDED AD) - uses AdManager
    // --------------------------------------------------------------------
    IEnumerator StartRunRoutine_WithAd()
    {
        
        bool adDone = false;
        playButton.interactable = false;

        if (AdManager.I != null)
        {
            //AdManager.I.HideBanner();

            AdManager.I.ShowRewarded(
                onSuccess: () => { adDone = true; },
                onFail: (err) => { Debug.Log("[GameManager] Play ad failed: " + err); adDone = true; }
            );
        }
        else
        {
            // no ad manager — proceed immediately
            adDone = true;
        }

        while (!adDone) yield return null;
        playButton.interactable = true;
        // After ad → proceed to normal start routine
        yield return StartCoroutine(StartRunRoutine());
    }

    // --------------------------------------------------------------------
    // REPLAY (WITH REWARDED AD) - uses AdManager
    // --------------------------------------------------------------------
    IEnumerator ReplayRoutine_WithAd()
    {
        bool adDone = false;

        if (AdManager.I != null)
        {
            //AdManager.I.HideBanner();

            AdManager.I.ShowRewarded(
                onSuccess: () => { adDone = true; },
                onFail: (err) => { Debug.Log("[GameManager] Replay ad failed: " + err); adDone = true; }
            );
        }
        else
        {
            adDone = true;
        }

        while (!adDone) yield return null;

        yield return StartCoroutine(ReplayRoutine());
    }

    // --------------------------------------------------------------------
    // ORIGINAL StartRunRoutine
    // --------------------------------------------------------------------
    IEnumerator StartRunRoutine()
    {
        if (startPanel) startPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        //if (AdManager.I != null) AdManager.I.HideBanner();

        if (crane)
        {
            crane.ResetCrane();
            crane.SetUIBlocking(true);
            crane.ClearPressState();
            yield return StartCoroutine(PlayStartCountdown(3));
            crane.ClearPressState();
            crane.SetUIBlocking(false);
        }

        IsRunning = true;
        Time.timeScale = 1f;

        if (showTutorialOnStart && tutorial)
            tutorial.ShowForSeconds(2.5f);
        showTutorialOnStart = false;
    }

    // --------------------------------------------------------------------
    // ORIGINAL ReplayRoutine
    // --------------------------------------------------------------------
    IEnumerator ReplayRoutine()
    {
        Time.timeScale = 1f;

        ClearAllFruits();

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

        //if (AdManager.I != null) AdManager.I.HideBanner();

        if (crane)
        {
            yield return StartCoroutine(PlayStartCountdown(3));
            crane.ClearPressState();
            crane.SetUIBlocking(false);
        }

        IsRunning = true;
        Time.timeScale = 1f;

        if (showTutorialOnStart && tutorial)
            tutorial.ShowForSeconds(2.5f);
    }

    // --------------------------------------------------------------------
    // SCORING
    // --------------------------------------------------------------------
    public void AddScore(int add)
    {
        score += Mathf.Max(0, add);
        if (scoreText) scoreText.text = score.ToString();

        int best = 0;
        if (DataManager.instance != null) best = DataManager.instance.GetInt(bestKey, 0);

        if (score > best)
        {
            if (bestText) bestText.text = score.ToString();
            if (DataManager.instance != null) DataManager.instance.SetInt(bestKey, score);
        }

        if (score >= nextScoreAd && !midgameAdRunning)
        {
            midgameAdRunning = true;

            if (AdManager.I != null)
            {
                AdManager.I.ShowMidgame(
                    onStart: () => { Debug.Log("[GameManager] Midgame ad started"); },
                    onFinish: () => { midgameAdRunning = false; },
                    onError: (err) =>
                    {
                        Debug.Log("[GameManager] Midgame ad error: " + err);
                        midgameAdRunning = false;
                    }
                );
            }
            else
            {
                // No AdManager: simply reset flag so ads don't block flow
                midgameAdRunning = false;
            }

            nextScoreAd += scoreAdInterval;
        }
    }

    // --------------------------------------------------------------------
    // GAME OVER
    // --------------------------------------------------------------------
    public void GameOver()
    {
        if (!IsRunning) return;

        IsRunning = false;
        Time.timeScale = 0f;

        int best = 0;
        if (DataManager.instance != null) best = DataManager.instance.GetInt(bestKey, 0);

        if (score > best)
        {
            best = score;
            if (DataManager.instance != null) DataManager.instance.SetInt(bestKey, best);
        }

        if (bestText) bestText.text = $"Best: {best}";
        if (finalScoreText) finalScoreText.text = score.ToString();

        if (gameOverPanel) gameOverPanel.SetActive(true);

        //if (showBannerInGameOver && AdManager.I != null) AdManager.I.ShowBanner();
    }

    // --------------------------------------------------------------------
    // CLEAR ALL FRUITS
    // --------------------------------------------------------------------
    void ClearAllFruits()
    {
        var fruits = FindObjectsOfType<Fruit>(includeInactive: false);
        foreach (var f in fruits)
            FruitFactory.Despawn(f.gameObject);
    }

    // --------------------------------------------------------------------
    // QUIT → MAIN MENU
    // --------------------------------------------------------------------
    public void QuitApp()
    {
#if UNITY_WEBGL
        Time.timeScale = 0f;
        IsRunning = false;

        ClearAllFruits();

        score = 0;
        if (scoreText) scoreText.text = "0";

        if (pausePanel) pausePanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (startPanel) startPanel.SetActive(true);

        //if (showBannerInMenu && AdManager.I != null)
        //    AdManager.I.ShowBanner();
#elif UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ----- Tiny pop animation for countdown (unscaled time) -----
    IEnumerator Pop(RectTransform rt, float duration, float startScale, float upScale, float endScale)
    {
        float t = 0f;
        Vector3 s0 = Vector3.one * startScale, sUp = Vector3.one * upScale, sEnd = Vector3.one * endScale;

        float upTime = duration * 0.7f;
        rt.localScale = s0;
        while (t < upTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / upTime);
            rt.localScale = Vector3.LerpUnclamped(s0, sUp, EaseOutBack(k));
            yield return null;
        }

        float downTime = duration - upTime;
        t = 0f;
        while (t < downTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / downTime);
            rt.localScale = Vector3.LerpUnclamped(sUp, sEnd, EaseOutQuad(k));
            yield return null;
        }

        rt.localScale = sEnd;
    }
    float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2); }
    float EaseOutQuad(float x) { return 1 - (1 - x) * (1 - x); }
}
