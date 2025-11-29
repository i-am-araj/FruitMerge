using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Spotlight mask material on a full-screen UI Image.
/// Call UpdateHoleForWorldTarget(transform, padding) or UpdateHoleForLocalRect(center, size)
/// to update the mask. Call Disable() to hide overlay.
/// </summary>
[DisallowMultipleComponent]
public class SpotlightMaskController : MonoBehaviour
{
    [Tooltip("The fullscreen Image using the UI/SpotlightMask material")]
    public Image overlayImage;

    Material runtimeMat;

    void Awake()
    {
        if (overlayImage == null) overlayImage = GetComponent<Image>();
        if (overlayImage == null)
        {
            Debug.LogWarning("SpotlightMaskController requires an Image with SpotlightMask material.");
            enabled = false;
            return;
        }
        // clone material instance so we don't affect shared material
        runtimeMat = Instantiate(overlayImage.material);
        overlayImage.material = runtimeMat;
    }

    /// <summary>
    /// Update hole based on a world-space transform. Padding is in world units (approx)
    /// To convert padding to normalized screen fraction we use screen extents heuristics.
    /// </summary>
    public void UpdateHoleForWorldTarget(Transform worldTarget, Vector2 padding)
    {
        if (runtimeMat == null || worldTarget == null) return;
        var parentCanvas = GetComponentInParent<Canvas>();
        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                     ? parentCanvas.worldCamera
                     : null;

        // compute bounds in world space
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

        // get center in screen 0..1
        float sx = (screenTL.x + screenBR.x) * 0.5f / Screen.width;
        float sy = (screenTL.y + screenBR.y) * 0.5f / Screen.height;

        // half-size normalized
        float hx = Mathf.Abs(screenBR.x - screenTL.x) * 0.5f / Screen.width;
        float hy = Mathf.Abs(screenBR.y - screenTL.y) * 0.5f / Screen.height;

        runtimeMat.SetVector("_HoleCenter", new Vector4(sx, sy, 0, 0));
        runtimeMat.SetVector("_HoleSize", new Vector4(hx, hy, 0, 0));
        runtimeMat.SetFloat("_UseRect", 0f); // ellipse by default
    }

    /// <summary>
    /// Update hole using canvas-local rect: center and size should be in local canvas units.
    /// center: local anchored center; size: width/height in canvas units
    /// </summary>
    public void UpdateHoleForLocalRect(Vector2 localCenter, Vector2 size)
    {
        var parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null || runtimeMat == null) return;

        RectTransform canvasRT = parentCanvas.transform as RectTransform;
        // convert localCenter relative to canvas rect (canvas space -> 0..1)
        Vector2 canvasSize = canvasRT.rect.size;
        float sx = (localCenter.x + canvasSize.x * 0.5f) / canvasSize.x;
        float sy = (localCenter.y + canvasSize.y * 0.5f) / canvasSize.y;

        float hx = size.x * 0.5f / canvasSize.x;
        float hy = size.y * 0.5f / canvasSize.y;

        runtimeMat.SetVector("_HoleCenter", new Vector4(sx, sy, 0, 0));
        runtimeMat.SetVector("_HoleSize", new Vector4(hx, hy, 0, 0));
        runtimeMat.SetFloat("_UseRect", 0f);
    }

    public void SetRectHole(bool useRect, float rounded = 0.2f)
    {
        if (runtimeMat == null) return;
        runtimeMat.SetFloat("_UseRect", useRect ? 1f : 0f);
        runtimeMat.SetFloat("_Rounded", rounded);
    }

    public void SetSoftness(float s) { if (runtimeMat != null) runtimeMat.SetFloat("_Softness", Mathf.Clamp01(s)); }
    public void SetOverlayColor(Color c) { if (runtimeMat != null) runtimeMat.SetColor("_Color", c); }
    public void SetBorder(float width, Color color) { if (runtimeMat != null) { runtimeMat.SetFloat("_BorderWidth", width); runtimeMat.SetColor("_BorderColor", color); } }

    public void Enable(bool on)
    {
        if (overlayImage != null) overlayImage.enabled = on;
    }
}
