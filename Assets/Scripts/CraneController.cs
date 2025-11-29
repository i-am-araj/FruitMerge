// Assets/Scripts/Crane/CraneController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

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
    [SerializeField] Image nextFruitImage;   // start-only countdown text (Raycast Target OFF)
    

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
        
    }
    public void ResetCrane()
    {
        //lastPointerWorldX = centerX;
        //transform.position = new Vector3(centerX, yHang, 0f);
        SpawnNew(); // spawns and hangs (will not fall until player releases after start)
        DecideNextFruit();
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

    

    // Decide Next Fruit
    GameObject nextFruit;
    void DecideNextFruit()
    {
        if (startingFruitPrefabs == null || startingFruitPrefabs.Length == 0) return;
        nextFruit = startingFruitPrefabs[Random.Range(0, startingFruitPrefabs.Length)];
        nextFruitImage.sprite=nextFruit.GetComponent<SpriteRenderer>().sprite;
    }
    // ----- Spawn & Drop -----
    void SpawnNew()
    {
        if (!nextFruit) 
        {
            if (startingFruitPrefabs == null || startingFruitPrefabs.Length == 0) return;
            nextFruit = startingFruitPrefabs[Random.Range(0, startingFruitPrefabs.Length)];
        }

        var go = FruitFactory.Spawn(nextFruit, new Vector3(transform.position.x, yHang, 0f));
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
            if (SoundManager.I != null) SoundManager.I.PlaySfx(SoundManager.SfxType.Drop);
        }

        carried = null;
        isHolding = false;

        // snap crane to center immediately
        //var pos = transform.position;
        //pos.x = centerX;
        //transform.position = pos;
        //lastPointerWorldX = centerX;

        // spawn next fruit after delay (no countdown here)
        Invoke(nameof(SpawnNew), Mathf.Max(0f, postDropDelaySeconds));
        DecideNextFruit();
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


    

}
