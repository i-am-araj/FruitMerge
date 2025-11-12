// Assets/Scripts/Crane/CraneController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public class CraneController : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] float minX = -3.4f;
    [SerializeField] float maxX = 3.4f;
    [SerializeField] float yHang = 9.0f;

    [Header("Movement")]
    [SerializeField] float followLerp = 15f;   // how quickly crane follows pointer
    [SerializeField] float centerX = 0f;       // snap-to center after each drop

    [Header("Spawn (first 4 fruits)")]
    [Tooltip("Drag your first 4 fruit prefabs here (smallest → larger).")]
    public GameObject[] startingFruitPrefabs;
    [SerializeField] float postDropDelaySeconds = 3f; // delay before next fruit appears

    [Header("UI")]
    [SerializeField] TMP_Text countdownText;   // start-only countdown text (Raycast Target OFF)

    [Header("Refs")]
    [SerializeField] Camera cam;               // assign or uses Camera.main

    // runtime
    GameObject carried;
    bool isHolding;

    bool isPressing;
    bool wasPressingLast;

    float lastPointerWorldX;
    bool blockUITouches = false;               // block during menus/countdown

    // ---- API for GameManager ----
    public void SetUIBlocking(bool on) => blockUITouches = on;
    public void ClearPressState() { isPressing = false; wasPressingLast = false; }

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (countdownText) countdownText.gameObject.SetActive(false);
    }

    void Start()
    {
        lastPointerWorldX = centerX;
        transform.position = new Vector3(centerX, yHang, 0f);
        SpawnNew(); // spawns and hangs (will not fall until player releases after start)
    }

    void Update()
    {
        wasPressingLast = isPressing;
        isPressing = IsPointerPressed();

        // follow pointer in unscaled time (still smooth during paused countdown)
        float desiredX = isPressing
            ? (lastPointerWorldX = Mathf.Clamp(ReadPointerWorldX(), minX, maxX))
            : lastPointerWorldX;

        var p = transform.position;
        p.x = Mathf.Lerp(p.x, desiredX, followLerp * Time.unscaledDeltaTime);
        transform.position = p;

        // drop only when game is running and player releases
        if (GameManager.I != null && GameManager.I.IsRunning)
        {
            bool justReleased = wasPressingLast && !isPressing;
            if (justReleased && isHolding) Drop();
        }
    }

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
    }

    // ----- Spawn & Drop -----
    void SpawnNew()
    {
        if (startingFruitPrefabs == null || startingFruitPrefabs.Length == 0) return;

        var prefab = startingFruitPrefabs[Random.Range(0, startingFruitPrefabs.Length)];
        if (!prefab) return;

        var go = FruitFactory.Spawn(prefab, new Vector3(transform.position.x, yHang, 0f));
        if (!go) return;

        carried = go;
        isHolding = true;

        var rb = carried.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Kinematic; // hang from crane (replaces isKinematic = true)
            rb.linearVelocity = Vector2.zero;              // replaces velocity = Vector2.zero
            rb.angularVelocity = 0f;
            rb.WakeUp();
        }

        carried.transform.SetParent(transform, true);
    }

    void Drop()
    {
        if (!carried) return;

        // release from crane
        carried.transform.SetParent(null, true);

        var rb = carried.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic; // replaces isKinematic = false
            rb.simulated = true;
            rb.WakeUp();
        }

        carried = null;
        isHolding = false;

        // snap crane to center immediately
        var pos = transform.position;
        pos.x = centerX;
        transform.position = pos;
        lastPointerWorldX = centerX;

        // spawn next fruit after delay (no countdown here)
        Invoke(nameof(SpawnNew), Mathf.Max(0f, postDropDelaySeconds));
    }

    // ----- Input helpers (Input System) -----
    bool IsPointerPressed()
    {
        if (blockUITouches) return false;
        if (PointerOverUI()) return false;

        bool touch = Touchscreen.current?.primaryTouch?.press.isPressed ?? false;
        bool mouse = Mouse.current?.leftButton?.isPressed ?? false;
        return touch || mouse;
    }

    bool PointerOverUI()
    {
        if (!EventSystem.current) return false;

        // Mouse
        if (Mouse.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        // Touch (primary)
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch != null && touch.press.isPressed)
            {
                // 0 = primary touch pointerId in default InputSystemUIInputModule
                if (EventSystem.current.IsPointerOverGameObject(0)) return true;
            }
        }
        return false;
    }

    float ReadPointerWorldX()
    {
        Vector2 screen;
        if (Touchscreen.current?.primaryTouch?.press.isPressed ?? false)
            screen = Touchscreen.current.primaryTouch.position.ReadValue();
        else
            screen = Mouse.current != null
                   ? Mouse.current.position.ReadValue()
                   : new Vector2(Screen.width * 0.5f, 0f);

        if (!cam) return transform.position.x;
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f)).x;
    }
    // === Add inside CraneController class ===

    // instantly recenter crane X (used by Replay)
    public void CenterCraneInstant()
    {
        lastPointerWorldX = centerX;
        var p = transform.position;
        p.x = centerX;
        transform.position = p;
    }

    // remove the currently carried fruit (if any) and cancel pending spawns
    public void ForceClearCarriedAndCancel()
    {
        CancelInvoke(nameof(SpawnNew)); // cancel delayed spawn if any

        if (carried != null)
        {
            // return carried fruit to pool
            FruitFactory.Despawn(carried);
            carried = null;
            isHolding = false;
        }
    }

    // public wrapper that spawns a new fruit right now (hanging)
    public void SpawnNewOnCrane()
    {
        CancelInvoke(nameof(SpawnNew));
        SpawnNew();
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
