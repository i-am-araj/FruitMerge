using UnityEngine;

/// <summary>
/// Reduce physics cost by:
/// 1) Continuous → Discrete after early impacts
/// 2) Sleeping bodies when calm
/// Skips sleeping while hanging (Kinematic) or not simulated.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FruitPhysicsTuner : MonoBehaviour
{
    [Header("Continuous → Discrete")]
    public float continuousDuration = 0.25f;

    [Header("Sleep settings")]
    public float sleepVelocity = 0.07f;  // linear speed threshold
    public float sleepTime = 0.7f;       // seconds calm before Sleep()

    Rigidbody2D rb;
    float bornAt;
    float calmSince = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ReArm();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.sleepMode = RigidbodySleepMode2D.StartAwake;
    }

    void Update()
    {
        if (rb == null) return;

        // ⚠ Do not sleep while hanging or disabled sim
        if (rb.bodyType == RigidbodyType2D.Kinematic || !rb.simulated)
        {
            calmSince = -1f;
            return;
        }

        // Step down to Discrete after the initial bouncy phase
        if (rb.collisionDetectionMode == CollisionDetectionMode2D.Continuous &&
            Time.time - bornAt >= continuousDuration)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        }

        // Sleep when calm
        if (rb.linearVelocity.sqrMagnitude < sleepVelocity * sleepVelocity &&
            Mathf.Abs(rb.angularVelocity) < 5f)
        {
            if (calmSince < 0f) calmSince = Time.time;
            else if (Time.time - calmSince >= sleepTime && rb.IsAwake())
                rb.Sleep();
        }
        else
        {
            calmSince = -1f;
            if (!rb.IsAwake()) rb.WakeUp();
        }
    }

    /// <summary>Call whenever the fruit is freshly spawned or merged.</summary>
    public void ReArm()
    {
        bornAt = Time.time;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.WakeUp();
    }
}
