using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class Fruit : MonoBehaviour
{
    [Header("Chain")]
    public int index = 0;                  // 0 = smallest
    public GameObject nextPrefab = null;   // null = largest (no merge)
    public int value = 1;                  // score when THIS fruit is created

    [Header("SFX (optional)")]
    public AudioClip mergeSfx;

    [Header("Merge Tuning")]
    [Tooltip("Ignore touches after spawn to prevent instant merges.")]
    public float graceSeconds = 0.10f;

    [Tooltip("Debounce time to prevent multiple merges in the same frame.")]
    public float mergeAttemptCooldown = 0.03f;


    // ===== runtime =====

    Rigidbody2D rb;
    Collider2D col;

    bool mergeLocked = false;
    float spawnGraceUntil = 0f;
    float lastMergeAttemptAt = -999f;

    // stable ID for deterministic tie-break (safe with pooling)
    static long _nextId = 1;
    long _id;

    // shared filter + buffer for Overlap detection
    static ContactFilter2D _filterConfigured;
    static bool _filterReady = false;

    static readonly List<Collider2D> _hits = new(16);


    // ===== lifecycle =====

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (!_filterReady)
        {
            _filterConfigured = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false,
                useDepth = false
            };
            _filterReady = true;
        }
    }

    void OnEnable()
    {
        mergeLocked = false;
        lastMergeAttemptAt = -999f;
        ArmGrace(graceSeconds);

        _id = _nextId++;   // monotonic, never reused
    }

    public void ArmGrace(float seconds)
    {
        spawnGraceUntil = Time.time + Mathf.Max(0f, seconds);
    }


    // ===== collision callbacks (fast path) =====

    void OnCollisionEnter2D(Collision2D c)
    {
        var other = c.collider.GetComponent<Fruit>();
        if (other) TryMerge(other);
    }

    void OnCollisionStay2D(Collision2D c)
    {
        var other = c.collider.GetComponent<Fruit>();
        if (other) TryMerge(other);
    }


    // ===== robust fallback: explicit overlap detection =====
    void FixedUpdate()
    {
        if (mergeLocked) return;
        if (Time.time < spawnGraceUntil) return;
        if (!col || !rb) return;

        _hits.Clear();
        int count = col.Overlap(_filterConfigured, _hits);
        if (count == 0) return;

        Fruit best = null;
        long bestOtherId = long.MinValue;

        for (int i = 0; i < count; i++)
        {
            var hc = _hits[i];
            if (!hc || hc == col) continue;

            // ignore triggers; need real geometry
            if (hc.isTrigger) continue;

            var fo = hc.GetComponent<Fruit>();
            if (!fo || fo.mergeLocked) continue;

            if (fo.index != index) continue;
            if (fo.spawnGraceUntil > Time.time) continue;

            // deterministic tie-break: choose highest id
            if (fo._id > bestOtherId)
            {
                bestOtherId = fo._id;
                best = fo;
            }
        }

        if (best != null)
            TryMerge(best);
    }


    // ===== merge logic =====

    void TryMerge(Fruit other)
    {
        // debounce
        if (Time.time - lastMergeAttemptAt < mergeAttemptCooldown) return;
        lastMergeAttemptAt = Time.time;

        if (mergeLocked) return;
        if (!other) return;
        if (other.mergeLocked) return;

        // grace
        if (Time.time < spawnGraceUntil) return;
        if (Time.time < other.spawnGraceUntil) return;

        // must be same level, must have next
        if (index != other.index) return;
        if (nextPrefab == null) return;

        // deterministic tie-break → only highest ID merges
        if (_id <= other._id) return;

        // lock
        mergeLocked = true;
        other.mergeLocked = true;

        Wake(rb);
        Wake(other.rb);

        // merge midpoint
        Vector3 mid = (transform.position + other.transform.position) * 0.5f;

        // spawn bigger fruit
        var bigger = FruitFactory.Spawn(nextPrefab, mid);

        // add small momentum-based impulse
        float momentum = 0f;
        if (rb) momentum += rb.mass * rb.linearVelocity.magnitude;
        if (other.rb) momentum += other.rb.mass * other.rb.linearVelocity.magnitude;

        var rbNew = bigger ? bigger.GetComponent<Rigidbody2D>() : null;
        if (rbNew)
            rbNew.AddForce(
                Vector2.up * Mathf.Clamp(momentum, 0.5f, 6f),
                ForceMode2D.Impulse
            );

        // re-bake collider paths & re-arm physics tuner
        var auto = bigger ? bigger.GetComponent<AutoPolygonFromSprite>() : null;
        if (auto) auto.BakeNow();

        var tuner = bigger ? bigger.GetComponent<FruitPhysicsTuner>() : null;
        if (tuner) tuner.ReArm();

        // scoring + sfx
        GameManager.I?.AddScore(Mathf.Max(1, value));
        if (mergeSfx) AudioSource.PlayClipAtPoint(mergeSfx, mid);

        // return originals to pool
        FruitFactory.Despawn(other.gameObject);
        FruitFactory.Despawn(gameObject);
    }


    // ===== helpers =====

    static void Wake(Rigidbody2D r)
    {
        if (!r) return;
        r.bodyType = RigidbodyType2D.Dynamic;  // replaces isKinematic = false
        if (!r.IsAwake()) r.WakeUp();
    }
}
