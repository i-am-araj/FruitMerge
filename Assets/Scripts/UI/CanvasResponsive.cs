using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Auto-adjust CanvasScaler.matchWidthOrHeight based on current aspect ratio to avoid UI stretching in WebGL/desktop.
/// Attach to the Canvas GameObject that has a CanvasScaler.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class CanvasResponsive : MonoBehaviour
{
    public Vector2 referenceResolution = new Vector2(1080, 1920); // portrait reference
    [Tooltip("If true, script will switch match between width(0) & height(1) based on current aspect.")]
    public bool autoMatch = true;

    CanvasScaler scaler;
    int lastW = 0;
    int lastH = 0;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            // keep initial match value as set in inspector
        }

        // initial apply
        TryAdjust();
    }

    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH)
            TryAdjust();
    }

    void TryAdjust()
    {
        lastW = Screen.width;
        lastH = Screen.height;

        if (!autoMatch || scaler == null) return;

        float screenAspect = (float)Screen.width / (float)Screen.height;
        float referenceAspect = referenceResolution.x / referenceResolution.y;

        // If screen is wider than reference (landscape browser) prefer match=0 (width),
        // otherwise prefer match=1 (height) for portrait.
        float match = screenAspect >= referenceAspect ? 0f : 1f;

        // Smooth option: if you want compromise use interpolation:
        // float match = Mathf.Clamp01(Mathf.InverseLerp(referenceAspect*0.9f, referenceAspect*1.1f, screenAspect));

        scaler.matchWidthOrHeight = match;

        // Force immediate rebuild
        Canvas.ForceUpdateCanvases();
    }
}
