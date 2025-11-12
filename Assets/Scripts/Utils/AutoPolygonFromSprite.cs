using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
[DisallowMultipleComponent]
public class AutoPolygonFromSprite : MonoBehaviour
{
    [Header("When to bake")]
    public bool bakeOnAwake = true;
    public bool bakeOnEnable = true;      // important for pooling
    public bool watchSpriteChange = true; // cheap reference check

    [Header("Source")]
    [Tooltip("Use Sprite Editor → Physics Shape if present.")]
    public bool useSpritePhysicsShape = true;

    [Tooltip("Fallback if no physics shape found.")]
    public FallbackMode fallback = FallbackMode.SpriteBoundsRectangle;

    public enum FallbackMode { SpriteBoundsRectangle }

    SpriteRenderer sr;
    PolygonCollider2D pc;
    Sprite lastSprite;

    static readonly List<Vector2> _shape = new();
    static readonly List<Vector2[]> _paths = new();

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        pc = GetComponent<PolygonCollider2D>();
        if (bakeOnAwake) BakeNow();
    }

    void OnEnable()
    {
        if (bakeOnEnable) BakeNow();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!pc) pc = GetComponent<PolygonCollider2D>();
        if (!Application.isPlaying) BakeNow();
    }
#endif

    void LateUpdate()
    {
        if (!watchSpriteChange) return;
        if (sr && sr.sprite != lastSprite) BakeNow();
    }

    public void BakeNow()
    {
        if (!sr || !pc) return;
        var sprite = sr.sprite;
        lastSprite = sprite;

        if (!sprite) { pc.pathCount = 0; return; }

        if (useSpritePhysicsShape && TryBakeFromPhysicsShape(sprite)) return;

        BakeFallback(sprite);
    }

    bool TryBakeFromPhysicsShape(Sprite sprite)
    {
        int num = sprite.GetPhysicsShapeCount();
        if (num <= 0) return false;

        _paths.Clear();
        for (int i = 0; i < num; i++)
        {
            _shape.Clear();
            sprite.GetPhysicsShape(i, _shape);
            _paths.Add(_shape.ToArray());
        }

        ApplyPaths(_paths);
        return true;
    }

    void BakeFallback(Sprite sprite)
    {
        var b = sprite.bounds;
        var p0 = new Vector2(b.min.x, b.min.y);
        var p1 = new Vector2(b.min.x, b.max.y);
        var p2 = new Vector2(b.max.x, b.max.y);
        var p3 = new Vector2(b.max.x, b.min.y);

        _paths.Clear();
        _paths.Add(new[] { p0, p1, p2, p3 });
        ApplyPaths(_paths);
    }

    void ApplyPaths(List<Vector2[]> paths)
    {
        pc.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++) pc.SetPath(i, paths[i]);
        pc.isTrigger = false;
        pc.autoTiling = false;
    }
}
