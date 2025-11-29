// CameraShake.cs
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    public static CameraShake I { get; private set; }

    [Header("Shake settings")]
    public float defaultDuration = 0.12f;
    public float defaultMagnitude = 0.12f;
    public float defaultRoughness = 0.8f; // not used directly, kept for future expansion

    Transform camTransform;
    Vector3 initialPos;
    Coroutine running;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        camTransform = transform;
        initialPos = camTransform.localPosition;
    }

    void OnEnable()
    {
        if (camTransform == null) camTransform = transform;
        initialPos = camTransform.localPosition;
    }

    /// <summary>Shake with defaults.</summary>
    public void Shake()
    {
        Shake(defaultDuration, defaultMagnitude);
    }

    /// <summary>Shake with custom parameters.</summary>
    public void Shake(float duration, float magnitude)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(DoShake(duration, magnitude));
    }

    IEnumerator DoShake(float duration, float magnitude)
    {
        float elapsed = 0f;
        initialPos = camTransform.localPosition;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            // smooth falloff
            float damper = 1.0f - Mathf.Clamp01(progress);

            // random point in unit circle
            Vector2 offset2 = Random.insideUnitCircle * magnitude * damper;
            camTransform.localPosition = initialPos + new Vector3(offset2.x, offset2.y, 0f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        camTransform.localPosition = initialPos;
        running = null;
    }
}
