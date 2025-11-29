using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class FruitPoolConfig : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public GameObject prefab;
        public int warmCount;
    }

    [Tooltip("Pre-instantiate these fruits into the pool on scene load.")]
    public Entry[] warmup = new Entry[]
    {
        // fill in Inspector (e.g., 10–20 each for smallest fruits)
    };

    void Awake()
    {
        foreach (var e in warmup)
            FruitFactory.Warm(e.prefab, Mathf.Max(0, e.warmCount));
    }
}
