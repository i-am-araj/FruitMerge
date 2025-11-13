using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial overlay that creates a rectangular "spotlight" (hole) using 4 dim panels
/// and animates left/right arrows. Uses robust canvas-local conversion and writes
/// offsets to full-stretch dim panels so there are no overlapping/gap issues.
/// </summary>
[DisallowMultipleComponent]
public class TutorialCoach : MonoBehaviour
{
    [Header("Core")]
    public CanvasGroup overlayGroup;    // the full-screen overlay group (same GameObject)
    [Tooltip("If true, overlay will not block raycasts so gameplay input still works.")]
    public bool nonBlocking = true;

    [Header("Spotlight")]
    [Tooltip("RectTransform that defines the spotlight (hole). Can be an empty RectTransform).")]
    public RectTransform spotlightArea; // empty RectTransform that defines the hole
    public RectTransform dimTop;        // assign 4 dim panels (Image) that form the hole
    public RectTransform dimBottom;
    public RectTransform dimLeft;
    public RectTransform dimRight;

    [Header("Arrows")]
    public RectTransform arrowLeft;     // optional
    public RectTransform arrowRight;    // optional
    public float arrowMove = 45f;
    public float arrowPeriod = 0.75f;   // seconds for ping-pong

    [Header("Fade / Auto")]
    public float fadeTime = 0.18f;
    [Tooltip("If >0 will auto-hide after this many seconds.")]
    public float showSeconds = 0f;

    [Header("Layout")]
    [Tooltip("Extra padding to expand the spotlight hole (positive expands hole).")]
    public Vector2 spotlightPadding = Vector2.zero;

    Coroutine wiggleCoroutine;
    Coroutine fadeCoroutine;
    bool visible;

    void Awake()
    {
        if (overlayGroup == null) overlayGroup = GetComponent<CanvasGroup>();
        // start hidden
        if (overlayGroup != null) { overlayGroup.alpha = 0f; overlayGroup.blocksRaycasts = false; overlayGroup.interactable = false; }
        SetArrowsActive(false);
        // initial layout (safe even if spotlightArea is null)
        LayoutDims();
    }

    void OnEnable()
    {
        LayoutDims();
    }

    void OnRectTransformDimensionsChange()
    {
        // called when canvas or rect sizes change
        if (visible) LayoutDims();
    }

    // PUBLIC API ------------------------------------------------

    /// <summary>Show indefinitely (or until Hide() or first drop).</summary>
    public void Show()
    {
        ShowInternal(0f);
    }

    /// <summary>Show for a fixed number of seconds (auto hide).</summary>
    public void ShowForSeconds(float seconds)
    {
        ShowInternal(seconds);
    }

    void ShowInternal(float seconds)
    {
        // layout before showing so dims are placed correctly
        LayoutDims();

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(1f, fadeTime));

        if (overlayGroup != null)
        {
            overlayGroup.blocksRaycasts = !nonBlocking; // if nonBlocking=true, we do NOT block raycasts
            overlayGroup.interactable = !nonBlocking;
        }

        SetArrowsActive(true);

        if (wiggleCoroutine != null) StopCoroutine(wiggleCoroutine);
        wiggleCoroutine = StartCoroutine(WiggleArrows(seconds));

        visible = true;
    }

    public void Hide()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(0f, fadeTime));

        SetArrowsActive(false);

        if (wiggleCoroutine != null) { StopCoroutine(wiggleCoroutine); wiggleCoroutine = null; }

        if (overlayGroup != null)
        {
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;
        }

        visible = false;
    }

    // LAYOUT (robust) ------------------------------------------
    // This implementation converts spotlight world corners to canvas-local coords
    // and then writes offsetMin/offsetMax into full-stretch dim panels so they
    // reliably carve a hole with no overlaps/gaps across all Canvas modes.

    /// <summary>
    /// Recomputes and applies the dim panel rectangles so a hole remains where spotlightArea is.
    /// Call this after you resize or move the spotlightArea at runtime or in editor.
    /// </summary>
    public void LayoutDims()
    {
        if (spotlightArea == null || dimTop == null || dimBottom == null || dimLeft == null || dimRight == null)
            return;

        RectTransform canvasRT = transform as RectTransform;
        if (canvasRT == null) return;

        // Determine which camera to use (works for Overlay / ScreenSpace-Camera / WorldSpace)
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                     ? parentCanvas.worldCamera
                     : null;

        // Get world corners of spotlight rect
        Vector3[] worldCorners = new Vector3[4];
        spotlightArea.GetWorldCorners(worldCorners);

        // Convert world corners to canvas-local (RectTransform) coordinates
        Vector2[] localCorners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 screenPt = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPt, cam, out localCorners[i]);
        }

        // Compute min/max in canvas-local space
        float minX = localCorners[0].x, maxX = localCorners[0].x;
        float minY = localCorners[0].y, maxY = localCorners[0].y;
        for (int i = 1; i < 4; i++)
        {
            if (localCorners[i].x < minX) minX = localCorners[i].x;
            if (localCorners[i].x > maxX) maxX = localCorners[i].x;
            if (localCorners[i].y < minY) minY = localCorners[i].y;
            if (localCorners[i].y > maxY) maxY = localCorners[i].y;
        }

        // apply padding (positive expands the hole)
        minX -= spotlightPadding.x;
        maxX += spotlightPadding.x;
        minY -= spotlightPadding.y;
        maxY += spotlightPadding.y;

        // full canvas rect in local space
        Rect full = canvasRT.rect;

        // IMPORTANT: dim panels SHOULD BE anchored to full-stretch: anchorMin=(0,0), anchorMax=(1,1)
        // We'll set offsets relative to those full-stretch anchors:

        // Top panel: covers from spotlight top (maxY) to canvas top (full.yMax)
        SetEdgeWithOffsets(dimTop, new Vector2(full.xMin, maxY), new Vector2(full.xMax, full.yMax), canvasRT);

        // Bottom panel: from canvas bottom to spotlight bottom (minY)
        SetEdgeWithOffsets(dimBottom, new Vector2(full.xMin, full.yMin), new Vector2(full.xMax, minY), canvasRT);

        // Left panel: from canvas left to spotlight left (minX), vertical range is minY..maxY
        SetEdgeWithOffsets(dimLeft, new Vector2(full.xMin, minY), new Vector2(minX, maxY), canvasRT);

        // Right panel: from spotlight right (maxX) to canvas right
        SetEdgeWithOffsets(dimRight, new Vector2(maxX, minY), new Vector2(full.xMax, maxY), canvasRT);
    }

    // Helper: writes offsetMin/offsetMax for a full-stretch child.
    void SetEdgeWithOffsets(RectTransform rt, Vector2 localMin, Vector2 localMax, RectTransform canvasRT)
    {
        if (rt == null || canvasRT == null) return;

        Rect full = canvasRT.rect;

        float left = localMin.x - full.xMin;
        float bottom = localMin.y - full.yMin;
        float right = localMax.x - full.xMax;
        float top = localMax.y - full.yMax;

        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    // ARROW ANIMATION -----------------------------------------

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
                float k = Mathf.PingPong(t, arrowPeriod) / arrowPeriod; // 0..1..0
                float dx = Mathf.Lerp(-arrowMove, arrowMove, k);
                arrowLeft.anchoredPosition = new Vector2(L0.x - Mathf.Abs(dx), L0.y);
            }
            if (arrowRight)
            {
                float k = Mathf.PingPong(t + arrowPeriod * 0.5f, arrowPeriod) / arrowPeriod; // offset phase
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

    // FADE ----------------------------------------------------

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
