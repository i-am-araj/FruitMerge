using System.Collections.Generic;
using UnityEngine;

public static class FruitFactory
{
    static readonly Dictionary<GameObject, Queue<GameObject>> _pool = new();
    static Transform _poolRoot;

    static Transform PoolRoot
    {
        get
        {
            if (_poolRoot == null)
            {
                var root = new GameObject("[FruitPool]");
                Object.DontDestroyOnLoad(root);
                _poolRoot = root.transform;
            }
            return _poolRoot;
        }
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position)
    {
        if (!prefab) return null;

        if (!_pool.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            _pool[prefab] = q;
        }

        GameObject go = null;
        while (q.Count > 0 && go == null) go = q.Dequeue();

        if (go == null)
        {
            go = Object.Instantiate(prefab);
            go.name = prefab.name;
            EnsureFruitComponents(go);
        }

        // Activate & reset
        go.transform.SetParent(null, true);
        go.transform.SetPositionAndRotation(position, Quaternion.identity);
        go.SetActive(true);

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
            rb.WakeUp();
        }

        // Rebuild polygon paths and re-arm physics tuner
        var auto = go.GetComponent<AutoPolygonFromSprite>();
        if (auto) auto.BakeNow();

        var fruit = go.GetComponent<Fruit>();
        if (fruit) fruit.ArmGrace(0.10f);

        var tuner = go.GetComponent<FruitPhysicsTuner>();
        if (tuner) tuner.ReArm();

        return go;
    }

    public static void Despawn(GameObject instance)
    {
        if (!instance) return;

        // Find bucket by prefab name (common case) or first available
        GameObject key = null;
        foreach (var kv in _pool)
        {
            if (kv.Key.name == instance.name) { key = kv.Key; break; }
        }
        if (key == null)
        {
            foreach (var kv in _pool) { key = kv.Key; break; }
            if (key == null) { Object.Destroy(instance); return; }
        }

        var rb = instance.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        instance.transform.SetParent(PoolRoot, false);
        instance.SetActive(false);

        _pool[key].Enqueue(instance);
    }

    public static void Warm(GameObject prefab, int count)
    {
        if (!prefab || count <= 0) return;

        if (!_pool.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            _pool[prefab] = q;
        }

        for (int i = 0; i < count; i++)
        {
            var go = Object.Instantiate(prefab);
            go.name = prefab.name;
            EnsureFruitComponents(go);

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb) rb.simulated = false;

            go.transform.SetParent(PoolRoot, false);
            go.SetActive(false);
            q.Enqueue(go);
        }
    }

    static void EnsureFruitComponents(GameObject go)
    {
        // Rigidbody
        var rb = go.GetComponent<Rigidbody2D>() ?? go.AddComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Replace Circle with Polygon
        var cc = go.GetComponent<CircleCollider2D>();
        if (cc) Object.Destroy(cc);

        var pc = go.GetComponent<PolygonCollider2D>() ?? go.AddComponent<PolygonCollider2D>();
        pc.isTrigger = false;
        pc.autoTiling = false;

        // Scripts (ensure present)
        if (!go.GetComponent<Fruit>()) go.AddComponent<Fruit>();
        if (!go.GetComponent<FruitPhysicsTuner>()) go.AddComponent<FruitPhysicsTuner>();
        if (!go.GetComponent<AutoPolygonFromSprite>()) go.AddComponent<AutoPolygonFromSprite>();
    }
}
