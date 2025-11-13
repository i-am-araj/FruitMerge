// Assets/Scripts/System/AdManager.cs
using System;
using System.Collections;
using UnityEngine;
using CrazyGames;

[DisallowMultipleComponent]
public class AdManager : MonoBehaviour
{
    public static AdManager I { get; private set; }

    [Header("Simulation (Editor)")]
    [Tooltip("If true, Editor will auto-succeed rewarded ads after simulationSeconds.")]
    public bool editorAutoSuccess = true;
    [Tooltip("Seconds for simulated ad duration in Editor.")]
    public float simulationSeconds = 2.0f;

    [Header("Rate limiting")]
    [Tooltip("Minimum seconds between any two rewarded ads.")]
    public float rewardedCooldown = 2.0f;
    [Tooltip("Minimum seconds between midgame ads.")]
    public float midgameCooldown = 1.0f;

    [Header("Ad control (toggle)")]
    [Tooltip("If false, AdManager will skip ads and immediately invoke callbacks.")]
    public bool adsEnabled = true;

    bool isAdShowing = false;
    float lastRewardedTime = -999f;
    float lastMidgameTime = -999f;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            CrazySDK.Init(() => Debug.Log("[AdManager] CrazySDK initialized (from AdManager)."));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AdManager] CrazySDK.Init threw: " + e);
        }
    }

    // -------------------------
    // Rewarded
    // -------------------------
    /// <summary>
    /// Show rewarded ad.
    /// - onSuccess invoked when ad finishes successfully or when ad is skipped and skipCountsAsSuccess = true.
    /// - onFail invoked with an error string when ad fails or skipCountsAsSuccess = false and ad is skipped.
    /// - forceShow: null = follow adsEnabled; true = force show; false = skip ad.
    /// - skipCountsAsSuccess: when skipping (ads disabled) should the manager call onSuccess (true) or onFail (false).
    /// </summary>
    public void ShowRewarded(Action onSuccess, Action<string> onFail = null, bool? forceShow = null, bool skipCountsAsSuccess = true)
    {
        // Determine final decision: show or skip
        bool shouldShow = ResolveShouldShow(forceShow);

        if (!shouldShow)
        {
            Debug.Log("[AdManager] Rewarded ad skipped by config.");
            // Immediately invoke the appropriate callback
            if (skipCountsAsSuccess)
                onSuccess?.Invoke();
            else
                onFail?.Invoke("ad_skipped_by_user");
            return;
        }

        if (isAdShowing)
        {
            Debug.Log("[AdManager] ShowRewarded blocked: another ad is showing");
            onFail?.Invoke("ad_already_showing");
            return;
        }

        if (Time.realtimeSinceStartup - lastRewardedTime < rewardedCooldown)
        {
            Debug.Log("[AdManager] ShowRewarded blocked: cooldown");
            onFail?.Invoke("cooldown");
            return;
        }

#if UNITY_EDITOR
        StartCoroutine(SimulateRewardedRoutine(onSuccess, onFail));
#else
        StartRealRewarded(onSuccess, onFail);
#endif
    }

    IEnumerator SimulateRewardedRoutine(Action onSuccess, Action<string> onFail)
    {
        isAdShowing = true;
        Debug.Log("[AdManager] Simulating rewarded ad (editor)...");
        yield return new WaitForSecondsRealtime(simulationSeconds);

        if (editorAutoSuccess)
        {
            lastRewardedTime = Time.realtimeSinceStartup;
            Debug.Log("[AdManager] Simulated rewarded -> success");
            onSuccess?.Invoke();
        }
        else
        {
            Debug.Log("[AdManager] Simulated rewarded -> failed");
            onFail?.Invoke("simulated_fail");
        }

        isAdShowing = false;
    }

    void StartRealRewarded(Action onSuccess, Action<string> onFail)
    {
        isAdShowing = true;
        bool callbackFired = false;

        try
        {
            CrazySDK.Ad.RequestAd(
                CrazyAdType.Rewarded,
                () =>
                {
                    Debug.Log("[AdManager] Rewarded started");
                },
                (error) =>
                {
                    string errMsg = ConvertErrorToString(error);
                    Debug.LogWarning("[AdManager] Rewarded error: " + errMsg);
                    if (!callbackFired)
                    {
                        callbackFired = true;
                        isAdShowing = false;
                        onFail?.Invoke(errMsg ?? "error");
                    }
                },
                () =>
                {
                    if (!callbackFired)
                    {
                        callbackFired = true;
                        lastRewardedTime = Time.realtimeSinceStartup;
                        isAdShowing = false;
                        onSuccess?.Invoke();
                    }
                }
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AdManager] Exception while requesting rewarded: " + e);
            isAdShowing = false;
            onFail?.Invoke("exception");
        }
    }

    // -------------------------
    // Midgame
    // -------------------------
    /// <summary>
    /// Show midgame ad. forceShow and skip behavior same as rewarded.
    /// </summary>
    public void ShowMidgame(Action onStart = null, Action onFinish = null, Action<string> onError = null, bool? forceShow = null, bool skipCountsAsSuccess = true)
    {
        bool shouldShow = ResolveShouldShow(forceShow);

        if (!shouldShow)
        {
            Debug.Log("[AdManager] Midgame ad skipped by config.");
            if (skipCountsAsSuccess)
                onFinish?.Invoke();
            else
                onError?.Invoke("ad_skipped_by_user");
            return;
        }

        if (isAdShowing)
        {
            Debug.Log("[AdManager] ShowMidgame blocked: ad already showing");
            onError?.Invoke("ad_already_showing");
            return;
        }

        if (Time.realtimeSinceStartup - lastMidgameTime < midgameCooldown)
        {
            Debug.Log("[AdManager] ShowMidgame blocked: cooldown");
            onError?.Invoke("cooldown");
            return;
        }

#if UNITY_EDITOR
        StartCoroutine(SimulateMidgameRoutine(onStart, onFinish, onError));
#else
        StartRealMidgame(onStart, onFinish, onError);
#endif
    }

    IEnumerator SimulateMidgameRoutine(Action onStart, Action onFinish, Action<string> onError)
    {
        isAdShowing = true;
        Debug.Log("[AdManager] Simulating midgame ad (editor)...");
        onStart?.Invoke();
        yield return new WaitForSecondsRealtime(simulationSeconds);
        lastMidgameTime = Time.realtimeSinceStartup;
        isAdShowing = false;
        Debug.Log("[AdManager] Simulated midgame finished");
        onFinish?.Invoke();
    }

    void StartRealMidgame(Action onStart, Action onFinish, Action<string> onError)
    {
        isAdShowing = true;
        bool cbFired = false;
        try
        {
            CrazySDK.Ad.RequestAd(
                CrazyAdType.Midgame,
                () =>
                {
                    Debug.Log("[AdManager] Midgame started");
                    onStart?.Invoke();
                },
                (error) =>
                {
                    string errMsg = ConvertErrorToString(error);
                    Debug.LogWarning("[AdManager] Midgame error: " + errMsg);
                    if (!cbFired)
                    {
                        cbFired = true;
                        isAdShowing = false;
                        onError?.Invoke(errMsg ?? "error");
                    }
                },
                () =>
                {
                    if (!cbFired)
                    {
                        cbFired = true;
                        lastMidgameTime = Time.realtimeSinceStartup;
                        isAdShowing = false;
                        onFinish?.Invoke();
                    }
                }
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AdManager] Exception requesting midgame: " + e);
            isAdShowing = false;
            onError?.Invoke("exception");
        }
    }
    // -------------------------
    // Utilities
    // -------------------------
    /// <summary>
    /// Determine whether to show an ad according to forceShow and adsEnabled.
    /// </summary>
    bool ResolveShouldShow(bool? forceShow)
    {
        if (forceShow.HasValue) return forceShow.Value;
        return adsEnabled;
    }

    static string ConvertErrorToString(object error)
    {
        if (error == null) return null;
        if (error is string s) return s;
        try { return error.ToString(); }
        catch { return "unknown_error"; }
    }

    public bool IsAdShowing()
    {
        return isAdShowing;
    }
}
