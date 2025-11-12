using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class TopLineLose : MonoBehaviour
{
    [Tooltip("How long a fruit must be nearly still while touching to lose.")]
    public float requiredStillTime = 0.8f;

    [Tooltip("Velocity magnitude below which the fruit counts as settled.")]
    public float settleVelocity = 0.22f;

    float timer;

    void Reset()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (GameManager.I == null || !GameManager.I.IsRunning) return;

        var rb = other.attachedRigidbody;
        var fruit = other.GetComponent<Fruit>();
        if (!rb || !fruit) { timer = 0f; return; }

        if (rb.linearVelocity.magnitude <= settleVelocity)
        {
            timer += Time.deltaTime;
            if (timer >= requiredStillTime)
                GameManager.I.GameOver();
        }
        else timer = 0f;
    }

    void OnTriggerExit2D(Collider2D _) => timer = 0f;
}
