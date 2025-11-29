// TutorialManager.cs
// Patched to use SpotlightMaskController (shader-based single-overlay hole) instead of 4-panel dims.
// Drop this file into Assets/Scripts/UI/TutorialManager.cs and assign the SpotlightMaskController overlay in the inspector.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    [Header("Canvas & Core")]
    public Canvas parentCanvas;               // recommended: Screen Space - Overlay
    public CanvasGroup overlayGroup;          // optional - used for fade in/out
    [Tooltip("If true, clicks outside the spotlight will pass through to underlying UI/game")]
    public bool nonBlocking = true;
    [Tooltip("If true, tutorial will pause the game (Time.timeScale = 0) while visible.")]
    public bool pauseGameDuringTutorial = false;
    public float fadeTime = 0.15f;

    [Header("Spotlight Mask (single overlay with shader)")]
    [Tooltip("Controller that updates the spotlight shader hole on the full-screen overlay image")]
    public SpotlightMaskController maskController;

    [Header("Spotlight and content (assign)")]
    public RectTransform spotlightArea;       // used to position content panel; size still controls content layout
    public RectTransform spotlightContentPanel; // panel shown inside spotlight
    public Image contentIcon;
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button nextButton;
    public Button skipButton;

    [Header("Arrows (optional)")]
    public RectTransform arrowLeft;
    public RectTransform arrowRight;
    public float arrowMove = 36f;
    public float arrowPeriod = 0.8f;

    [Header("Step list (configure in inspector)")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    [Serializable]
    public class TutorialStep
    {
        public string title = "Title";
        [TextArea(2, 4)] public string description = "Description";
        public Sprite icon = null;
        [Tooltip("World target to follow (optional). If null, uses localRect.")]
        public Transform worldTarget = null;
        [Tooltip("Padding around world target in world units.")]
        public Vector2 worldPadding = new Vector2(0.3f, 0.3f);
        [Tooltip("If worldTarget is null, use this local center/size. (x,y, width, height)")]
        public Vector4 localRect = new Vector4(0, 0, 300, 180);
        [Tooltip("If > 0, auto-advance after this many seconds (uses unscaled time). If 0, waits for Next button.")]
        public float durationSeconds = 3.5f;
        public bool showArrows = true;
        public bool followTarget = true;
    }

    // Events
    public event Action OnTutorialStarted;
    public event Action OnTutorialFinished;
    public event Action<int> OnStepChanged;
    public event Action OnTutorialSkipped;

    // runtime
    int currentStep = -1;
    Coroutine sequenceCoroutine;
    Coroutine fadeCoroutine;
    Coroutine wiggleCoroutine;
    Coroutine autoHideCoroutine;
    bool visible = false;

    // user input flags (robust)
    bool userRequestedNext = false;
    bool userRequestedSkip = false;

    const string PREF_TUTORIAL_SHOWN = "SUIKA_TUTORIAL_SHOWN";

    void Reset()
    {
        nonBlocking = true;
        pauseGameDuringTutorial = false;
        fadeTime = 0.15f;
    }

    void Awake()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (overlayGroup == null) overlayGroup = GetComponent<CanvasGroup>();

        if (overlayGroup != null)
        {
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;
        }

        // ensure buttons wired to safe handlers
        if (nextButton != null) nextButton.onClick.AddListener(() => { userRequestedNext = true; });
        if (skipButton != null) skipButton.onClick.AddListener(() => { userRequestedSkip = true; Hide(); OnTutorialSkipped?.Invoke(); });

        // hide content initially
        if (spotlightContentPanel != null) spotlightContentPanel.gameObject.SetActive(false);

        // default simple step if none provided (convenience)
        if (steps == null || steps.Count == 0)
        {
            steps = new List<TutorialStep>()
            {
                new TutorialStep(){ title="Move the Crane", description="Drag left/right to move the crane. Release to drop the fruit.", localRect=new Vector4(0,180,600,220), durationSeconds=3.5f, showArrows=true },
                new TutorialStep(){ title="Drop the Fruit", description="Release to drop fruits into the box. Merge same fruits to grow them.", localRect=new Vector4(0,-120,700,300), durationSeconds=4f, showArrows=false }
            };
        }

        // ensure maskController exists and overlay image enabled state
        if (maskController != null)
            maskController.Enable(false);
    }

    void OnEnable()
    {
        // nothing special — mask is controlled per-step
    }

    void OnRectTransformDimensionsChange()
    {
        if (visible)
        {
            // reposition content for current step
            if (currentStep >= 0 && currentStep < steps.Count) ApplyStepMaskAndContent(steps[currentStep]);
        }
    }

    void LateUpdate()
    {
        if (!visible) return;
        if (currentStep < 0 || currentStep >= steps.Count) return;
        var s = steps[currentStep];
        if (s.worldTarget != null && s.followTarget && maskController != null)
        {
            // update mask live
            maskController.UpdateHoleForWorldTarget(s.worldTarget, s.worldPadding);
            // update content panel position too
            UpdateContentPositionFromMask();
        }
    }

    // ---------------- Public API ----------------

    public bool IsVisible => visible;

    public void ShowSequence()
    {
        if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(SequenceRoutine());
    }

    public void ShowStep(int index)
    {
        if (index < 0 || index >= steps.Count) return;
        if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = StartCoroutine(ShowSingleStep(index));
    }

    public void ShowForSeconds(float seconds)
    {
        if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
        ShowInternal(seconds);
    }

    /// <summary>Show sequence only if not shown before (first run)</summary>
    public void ShowOnceForFirstRun()
    {
        if (PlayerPrefs.GetInt(PREF_TUTORIAL_SHOWN, 0) == 0)
        {
            ShowSequence();
            PlayerPrefs.SetInt(PREF_TUTORIAL_SHOWN, 1);
            PlayerPrefs.Save();
        }
    }

    public void Hide()
    {
        if (!visible) return;

        if (sequenceCoroutine != null) { StopCoroutine(sequenceCoroutine); sequenceCoroutine = null; }
        if (autoHideCoroutine != null) { StopCoroutine(autoHideCoroutine); autoHideCoroutine = null; }
        if (wiggleCoroutine != null) { StopCoroutine(wiggleCoroutine); wiggleCoroutine = null; }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(0f, fadeTime));

        SetArrowsActive(false);

        if (overlayGroup != null) { overlayGroup.blocksRaycasts = false; overlayGroup.interactable = false; }
        SetMaskRaycast(!nonBlocking); // restore blocking default

        if (maskController != null) maskController.Enable(false);

        if (pauseGameDuringTutorial) Time.timeScale = 1f;

        visible = false;
        currentStep = -1;

        OnTutorialFinished?.Invoke();
    }

    public void Skip()
    {
        userRequestedSkip = true;
        Hide();
        OnTutorialSkipped?.Invoke();
    }

    public void Next()
    {
        userRequestedNext = true;
    }

    public void SetNonBlocking(bool allow)
    {
        nonBlocking = allow;
        SetMaskRaycast(!nonBlocking);
    }

    // ---------------- Sequence implementation ----------------

    IEnumerator SequenceRoutine()
    {
        if (steps == null || steps.Count == 0) yield break;

        ShowInternal(0f);

        if (pauseGameDuringTutorial) Time.timeScale = 0f;

        currentStep = 0;
        OnTutorialStarted?.Invoke();

        while (currentStep < steps.Count)
        {
            yield return StartCoroutine(ShowStepInternal(currentStep));
            if (userRequestedSkip) break;
            currentStep++;
        }

        Hide();
        sequenceCoroutine = null;
    }

    IEnumerator ShowSingleStep(int index)
    {
        ShowInternal(0f);
        OnTutorialStarted?.Invoke();
        currentStep = index;
        yield return StartCoroutine(ShowStepInternal(index));
        Hide();
    }

    void ShowInternal(float seconds)
    {
        userRequestedNext = false;
        userRequestedSkip = false;
        visible = true;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(1f, fadeTime));

        if (overlayGroup != null)
        {
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = true;
        }

        SetMaskRaycast(!nonBlocking);

        if (spotlightContentPanel != null)
        {
            spotlightContentPanel.gameObject.SetActive(true);
            var cg = spotlightContentPanel.GetComponent<CanvasGroup>();
            if (cg == null) spotlightContentPanel.gameObject.AddComponent<CanvasGroup>();
        }

        if (seconds > 0f) autoHideCoroutine = StartCoroutine(AutoHideAfter(seconds));
    }

    IEnumerator AutoHideAfter(float sec)
    {
        yield return new WaitForSecondsRealtime(sec);
        Hide();
        autoHideCoroutine = null;
    }

    IEnumerator ShowStepInternal(int idx)
    {
        if (idx < 0 || idx >= steps.Count) yield break;
        var s = steps[idx];

        // populate content
        if (titleText != null) titleText.text = s.title ?? "";
        if (descText != null) descText.text = s.description ?? "";
        if (contentIcon != null) { contentIcon.sprite = s.icon; contentIcon.gameObject.SetActive(s.icon != null); }

        // apply mask and position content
        ApplyStepMaskAndContent(s);

        SetArrowsActive(s.showArrows);
        if (wiggleCoroutine != null) StopCoroutine(wiggleCoroutine);
        wiggleCoroutine = StartCoroutine(WiggleArrows(s.durationSeconds));

        // ensure buttons visible/interactable
        if (nextButton != null) { nextButton.gameObject.SetActive(true); nextButton.interactable = true; }
        if (skipButton != null) { skipButton.gameObject.SetActive(true); skipButton.interactable = true; }

        OnStepChanged?.Invoke(idx);

        // waiting logic
        userRequestedNext = false;
        userRequestedSkip = false;
        float elapsed = 0f;

        if (s.durationSeconds <= 0f)
        {
            while (!userRequestedNext && !userRequestedSkip) yield return null;
        }
        else
        {
            while (elapsed < s.durationSeconds && !userRequestedNext && !userRequestedSkip)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // cleanup
        if (wiggleCoroutine != null) { StopCoroutine(wiggleCoroutine); wiggleCoroutine = null; }
        SetArrowsActive(false);
        yield break;
    }

    // ---------------- Mask + content helpers ----------------

    void ApplyStepMaskAndContent(TutorialStep s)
    {
        if (maskController == null)
        {
            // fallback: position spotlightArea only (no mask)
            if (s.worldTarget != null)
            {
                PositionSpotlightOverWorldTargetLocal(s.worldTarget, s.worldPadding);
            }
            else
            {
                PositionSpotlightLocal(s.localRect);
            }
            return;
        }

        maskController.Enable(true);

        if (s.worldTarget != null)
        {
            maskController.UpdateHoleForWorldTarget(s.worldTarget, s.worldPadding);
            // set content panel anchored position to center of hit area
            UpdateContentPositionFromMask();
        }
        else
        {
            // compute local center & size from localRect and set mask accordingly
            Vector2 localCenter = new Vector2(s.localRect.x, s.localRect.y);
            Vector2 size = new Vector2(s.localRect.z, s.localRect.w);
            maskController.UpdateHoleForLocalRect(localCenter, size);
            // position content panel to this center
            SetContentAnchoredPosition(localCenter);
        }
    }

    void SetMaskRaycast(bool block)
    {
        if (maskController == null) return;
        // maskController manipulates the Image's raycastTarget through the material usage; ensure overlay image still blocks or not
        var img = maskController.GetComponent<Image>();
        if (img != null) img.raycastTarget = block;
    }

    void UpdateContentPositionFromMask()
    {
        if (maskController == null || spotlightContentPanel == null || parentCanvas == null)
            return;

        if (currentStep < 0 || currentStep >= steps.Count)
            return;

        var s = steps[currentStep];
        if (s.worldTarget == null)
            return;

        Camera cam = (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                     ? parentCanvas.worldCamera
                     : null;

        // Get world bounds of the target (same as mask)
        Bounds b = new Bounds(s.worldTarget.position, Vector3.zero);

        var rt = s.worldTarget as RectTransform;
        if (rt != null)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++) b.Encapsulate(corners[i]);
        }
        else
        {
            var rend = s.worldTarget.GetComponentInChildren<Renderer>();
            if (rend != null) b = rend.bounds;
            else
            {
                var col2 = s.worldTarget.GetComponentInChildren<Collider2D>();
                if (col2 != null) b = new Bounds(col2.bounds.center, col2.bounds.size);
                else b = new Bounds(s.worldTarget.position, Vector3.one * 0.5f);
            }
        }

        b.Expand(new Vector3(s.worldPadding.x, s.worldPadding.y, 0f));

        // Convert to screen
        Vector3 worldTL = new Vector3(b.min.x, b.max.y, b.min.z);
        Vector3 worldBR = new Vector3(b.max.x, b.min.y, b.max.z);

        Vector2 screenTL = RectTransformUtility.WorldToScreenPoint(cam, worldTL);
        Vector2 screenBR = RectTransformUtility.WorldToScreenPoint(cam, worldBR);

        // Convert to local canvas space
        RectTransform canvasRT = parentCanvas.transform as RectTransform;

        float sx = (screenTL.x + screenBR.x) * 0.5f / Screen.width;
        float sy = (screenTL.y + screenBR.y) * 0.5f / Screen.height;

        Vector2 canvasSize = canvasRT.rect.size;
        Vector2 localCenter = new Vector2(
            sx * canvasSize.x - canvasSize.x * 0.5f,
            sy * canvasSize.y - canvasSize.y * 0.5f
        );

        spotlightContentPanel.anchoredPosition = localCenter;
    }


    void SetContentAnchoredPosition(Vector2 anchoredLocalCenter)
    {
        if (spotlightContentPanel == null) return;
        spotlightContentPanel.anchoredPosition = anchoredLocalCenter;
    }

    // Legacy fallback: compute local rect position when maskController is not available
    bool PositionSpotlightOverWorldTargetLocal(Transform worldTarget, Vector2 padding)
    {
        if (spotlightArea == null || parentCanvas == null || worldTarget == null) return false;
        var canvasRT = parentCanvas.transform as RectTransform;
        if (canvasRT == null) return false;

        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;

        Bounds b = new Bounds(worldTarget.position, Vector3.zero);
        var rt = worldTarget as RectTransform;
        if (rt != null)
        {
            Vector3[] corners = new Vector3[4]; rt.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++) b.Encapsulate(corners[i]);
        }
        else
        {
            var rend = worldTarget.GetComponentInChildren<Renderer>();
            if (rend != null) b = rend.bounds;
            else
            {
                var col2 = worldTarget.GetComponentInChildren<Collider2D>();
                if (col2 != null) b = new Bounds(col2.bounds.center, col2.bounds.size);
                else b = new Bounds(worldTarget.position, Vector3.one * 0.5f);
            }
        }

        b.Expand(new Vector3(padding.x, padding.y, 0f));

        Vector3 worldTL = new Vector3(b.min.x, b.max.y, b.min.z);
        Vector3 worldBR = new Vector3(b.max.x, b.min.y, b.max.z);

        Vector2 screenTL = RectTransformUtility.WorldToScreenPoint(cam, worldTL);
        Vector2 screenBR = RectTransformUtility.WorldToScreenPoint(cam, worldBR);

        Vector2 localTL, localBR;
        bool okA = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenTL, cam, out localTL);
        bool okB = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenBR, cam, out localBR);
        if (!okA || !okB) return false;

        Vector2 center = (localTL + localBR) * 0.5f;
        Vector2 size = new Vector2(Mathf.Abs(localBR.x - localTL.x), Mathf.Abs(localBR.y - localTL.y));
        if (size.x < 10f) size.x = 10f;
        if (size.y < 10f) size.y = 10f;

        spotlightArea.anchoredPosition = center;
        spotlightArea.sizeDelta = size;
        SetContentAnchoredPosition(center);
        return true;
    }

    void PositionSpotlightLocal(Vector4 localRect)
    {
        if (spotlightArea == null) return;
        spotlightArea.anchoredPosition = new Vector2(localRect.x, localRect.y);
        spotlightArea.sizeDelta = new Vector2(localRect.z, localRect.w);
        SetContentAnchoredPosition(new Vector2(localRect.x, localRect.y));
    }

    // arrows
    void SetArrowsActive(bool on)
    {
        if (arrowLeft) arrowLeft.gameObject.SetActive(on);
        if (arrowRight) arrowRight.gameObject.SetActive(on);
    }

    IEnumerator WiggleArrows(float timeoutSeconds)
    {
        float elapsed = 0f;
        Vector2 L0 = arrowLeft ? arrowLeft.anchoredPosition : Vector2.zero;
        Vector2 R0 = arrowRight ? arrowRight.anchoredPosition : Vector2.zero;
        float t = 0f;

        while (true)
        {
            if (arrowLeft)
            {
                float k = Mathf.PingPong(t, arrowPeriod) / arrowPeriod;
                float dx = Mathf.Lerp(-arrowMove, arrowMove, k);
                arrowLeft.anchoredPosition = new Vector2(L0.x - Mathf.Abs(dx), L0.y);
            }
            if (arrowRight)
            {
                float k = Mathf.PingPong(t + arrowPeriod * 0.5f, arrowPeriod) / arrowPeriod;
                float dx = Mathf.Lerp(-arrowMove, arrowMove, k);
                arrowRight.anchoredPosition = new Vector2(R0.x + Mathf.Abs(dx), R0.y);
            }

            t += Time.unscaledDeltaTime;
            if (timeoutSeconds > 0f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= timeoutSeconds) { Hide(); yield break; }
            }

            yield return null;
        }
    }

    IEnumerator FadeTo(float target, float dur)
    {
        if (overlayGroup == null) yield break;
        float start = overlayGroup.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(start, target, dur > 0f ? t / dur : 1f);
            yield return null;
        }
        overlayGroup.alpha = target;
    }
}
