// MergeVfxController.cs
using System.Collections.Generic;
using UnityEngine;

public class MergeVfxController : MonoBehaviour
{
    public static MergeVfxController I { get; private set; }

    [Header("Particle prefab (must contain ParticleSystem)")]
    public GameObject mergeParticlePrefab;

    [Header("Pooling")]
    public int poolSize = 8;

    Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        // init pool
        for (int i = 0; i < Mathf.Max(1, poolSize); i++)
        {
            CreatePooled();
        }
    }

    GameObject CreatePooled()
    {
        if (mergeParticlePrefab == null) return null;
        var go = Instantiate(mergeParticlePrefab, transform);
        go.SetActive(false);
        pool.Enqueue(go);
        return go;
    }

    GameObject GetFromPool()
    {
        if (mergeParticlePrefab == null) return null;
        if (pool.Count == 0) CreatePooled();
        return pool.Dequeue();
    }

    void ReturnToPool(GameObject go)
    {
        go.SetActive(false);
        pool.Enqueue(go);
    }

    /// <summary>
    /// Play merge VFX at world position. Optionally pass a color to tint the particles.
    /// </summary>
    public void PlayMergeVfx(Vector3 worldPos, Color? tint = null, float playDuration = 0.9f)
    {
        if (mergeParticlePrefab == null) return;

        var go = GetFromPool();
        if (go == null) return;

        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.identity;
        go.SetActive(true);

        // apply tint if requested: find ParticleSystem Main and set startColor OR SpriteRenderer if used
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null && tint.HasValue)
        {
            var main = ps.main;
            main.startColor = tint.Value;
        }

        // Play particle
        if (ps != null) ps.Play();

        // auto-return to pool after duration
        StartCoroutine(ReturnAfter(go, playDuration));
    }

    System.Collections.IEnumerator ReturnAfter(GameObject go, float t)
    {
        yield return new WaitForSecondsRealtime(t);
        // ensure stop
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        ReturnToPool(go);
    }
}
