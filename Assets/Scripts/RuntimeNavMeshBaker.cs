using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bakes a NavMesh from the colliders under <see cref="geometryRoot"/> (plus <see cref="extraGeometry"/>)
/// when the scene starts. The bake volume is clipped to <see cref="bakeArea"/> on XZ (the inside of the
/// outer walls) and just below the ceiling, so nothing outside the Backrooms or on wall tops is walkable.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class RuntimeNavMeshBaker : MonoBehaviour
{
    public Transform geometryRoot;
    [Tooltip("Additional collider roots baked into the NavMesh (e.g. invisible boundary walls).")]
    public Transform[] extraGeometry;
    [Tooltip("World-space XZ area to bake. Leave size at zero to use the bounds of all geometry.")]
    public Bounds bakeArea;
    [Tooltip("How high above the lowest floor the bake volume reaches. Keep it below the ceiling.")]
    public float walkableVolumeHeight = 2.3f;
    public int agentTypeID = 0;

    [Header("Agent size used for the bake (0 = project agent settings)")]
    public float agentRadius = 0.3f;
    public float agentHeight = 1.48f;

    public static bool IsReady { get; private set; }
    public static Bounds WalkBounds { get; private set; }

    NavMeshDataInstance instance;

    void Awake()
    {
        Build();
    }

    public void Build()
    {
        IsReady = false;
        if (instance.valid) instance.Remove();

        var roots = new List<Transform> { geometryRoot != null ? geometryRoot : transform };
        if (extraGeometry != null)
            foreach (var t in extraGeometry)
                if (t != null) roots.Add(t);

        var sources = new List<NavMeshBuildSource>();
        bool hasGeo = false;
        Bounds geo = default;
        foreach (var root in roots)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>())
            {
                if (!hasGeo) { geo = c.bounds; hasGeo = true; }
                else geo.Encapsulate(c.bounds);
            }
            var part = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), part);
            sources.AddRange(part);
        }
        if (!hasGeo)
        {
            Debug.LogError("RuntimeNavMeshBaker: no colliders to bake");
            return;
        }

        float floorY = geo.min.y;
        bool useArea = bakeArea.size.x > 0f && bakeArea.size.z > 0f;
        var xz = useArea ? bakeArea : geo;
        var min = new Vector3(xz.min.x, floorY - 0.5f, xz.min.z);
        var max = new Vector3(xz.max.x, floorY + walkableVolumeHeight, xz.max.z);
        var bounds = new Bounds((min + max) * 0.5f, max - min);
        WalkBounds = bounds;

        var settings = NavMesh.GetSettingsByID(agentTypeID);
        if (agentRadius > 0f) settings.agentRadius = agentRadius;
        if (agentHeight > 0f) settings.agentHeight = agentHeight;

        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null)
        {
            Debug.LogError("RuntimeNavMeshBaker: bake failed");
            return;
        }

        instance = NavMesh.AddNavMeshData(data);
        IsReady = true;
    }

    void OnDestroy()
    {
        if (instance.valid) instance.Remove();
        IsReady = false;
    }

    void OnDrawGizmosSelected()
    {
        if (bakeArea.size.x <= 0f) return;
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(bakeArea.center, bakeArea.size);
    }
}
