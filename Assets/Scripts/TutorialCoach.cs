using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TutorialCoach : MonoBehaviour
{
    [Header("Overlay Group")]
    public CanvasGroup overlayGroup;           // assign TutorialOverlay (this object)
    [Tooltip("If true, the coach will not consume UI raycasts (gameplay remains interactive).")]
    public bool nonBlocking = true;

    [Header("Spotlight Rect (in canvas space)")]
    public RectTransform spotlightArea;        // an invisible RectTransform defining the 'hole'
    public RectTransform dimTop, dimBottom, dimLeft, dimRight;

    [Header("Arrows")]
    public RectTransform arrowLeft;
    public RectTransform arrowRight;
    public float arrowMove = 45f;
    public float arrowPeriod = 0.75f;
    public float fadeTime = 0.2f;

    [Header("Auto Hide")]
    public float showSeconds = 0f;             // 0 = manual hide; >0 = auto hide after seconds

    Coroutine _run;
    bool _visible;

    void Awake()
    {
        SetVisible(false, instant: true);
        LayoutDims();
    }

    void OnRectTransformDimensionsChange()
    {
        if (_visible) LayoutDims();
    }

    // Public API
    public void Show()
    {
        if (_run != null) StopCoroutine(_run);
        SetVisible(true, instant: false);
        _run = StartCoroutine(WiggleArrows());
    }

    public void ShowForSeconds(float seconds)
    {
        showSeconds = seconds;
        Show();
    }

    public void Hide()
    {
        if (_run != null) { StopCoroutine(_run); _run = null; }
        SetVisible(false, instant: false);
    }

    public void SetSpotlightRect(RectTransform worldAnchoredRect)
    {
        // Optional helper if you want to pass a different rect at runtime
        spotlightArea = worldAnchoredRect;
        LayoutDims();
    }

    // Position the 4 dim panels around spotlightArea (creates the “hole”)
    void LayoutDims()
    {
        if (!spotlightArea || !dimTop || !dimBottom || !dimLeft || !dimRight) return;

        var canvas = GetComponentInParent<Canvas>();
        if (!canvas) return;

        // Spotlight rect in Overlay canvas space
        var spMin = spotlightArea.TransformPoint(spotlightArea.rect.min);
        var spMax = spotlightArea.TransformPoint(spotlightArea.rect.max);

        Vector2 spMinCanvas, spMaxCanvas;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, RectTransformUtility.WorldToScreenPoint(null, spMin), null, out spMinCanvas);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, RectTransformUtility.WorldToScreenPoint(null, spMax), null, out spMaxCanvas);

        var canvasRT = (RectTransform)transform;
        var full = canvasRT.rect;

        // Top: from spotlight top to canvas top
        SetEdge(dimTop, new Vector2(full.xMin, spMaxCanvas.y), new Vector2(full.xMax, full.yMax));
        // Bottom: from canvas bottom to spotlight bottom
        SetEdge(dimBottom, new Vector2(full.xMin, full.yMin), new Vector2(full.xMax, spMinCanvas.y));
        // Left: from canvas left to spotlight left
        SetEdge(dimLeft, new Vector2(full.xMin, spMinCanvas.y), new Vector2(spMinCanvas.x, spMaxCanvas.y));
        // Right: from spotlight right to canvas right
        SetEdge(dimRight, new Vector2(spMaxCanvas.x, spMinCanvas.y), new Vector2(full.xMax, spMaxCanvas.y));
    }

    void SetEdge(RectTransform rt, Vector2 localMin, Vector2 localMax)
    {
        var size = localMax - localMin;
        rt.anchoredPosition = localMin + size * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
    }

    void SetVisible(bool on, bool instant)
    {
        _visible = on;
        if (!overlayGroup) return;

        if (instant)
        {
            overlayGroup.alpha = on ? 1f : 0f;
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(FadeTo(on ? 1f : 0f, fadeTime));
        }

        // nonBlocking=true means do NOT eat raycasts
        overlayGroup.blocksRaycasts = !nonBlocking && on;
        overlayGroup.interactable = !nonBlocking && on;

        if (arrowLeft) arrowLeft.gameObject.SetActive(on);
        if (arrowRight) arrowRight.gameObject.SetActive(on);
    }

    IEnumerator FadeTo(float target, float dur)
    {
        float start = overlayGroup.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        overlayGroup.alpha = target;
    }

    IEnumerator WiggleArrows()
    {
        float t = 0f;
        float elapsed = 0f;
        Vector2 L0 = arrowLeft ? arrowLeft.anchoredPosition : Vector2.zero;
        Vector2 R0 = arrowRight ? arrowRight.anchoredPosition : Vector2.zero;

        while (_visible)
        {
            t += Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;

            float k = Mathf.PingPong(t, arrowPeriod) / arrowPeriod; // 0..1..0
            float dx = Mathf.Lerp(-arrowMove, arrowMove, k);

            if (arrowLeft) arrowLeft.anchoredPosition = new Vector2(L0.x - Mathf.Abs(dx), L0.y);
            if (arrowRight) arrowRight.anchoredPosition = new Vector2(R0.x + Mathf.Abs(dx), R0.y);

            if (showSeconds > 0f && elapsed >= showSeconds) { Hide(); yield break; }
            yield return null;
        }
    }
}
