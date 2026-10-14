using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in room volume. Assign only roof renderers, never the whole building.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Nusantara/Visibility/Room Roof Visibility")]
public sealed class RoomRoofVisibility : MonoBehaviour
{
    public Transform target;
    [Tooltip("Room volume in this object's local space. No physics collider is required.")]
    public Vector3 areaCenter = new Vector3(0f, 2f, 0f);
    public Vector3 areaSize = new Vector3(10f, 6f, 10f);
    [Min(0f), Tooltip("Extra distance before restoring the roof, to prevent doorway flicker.")]
    public float exitPadding = 0.25f;
    [Tooltip("Explicit roof pieces to hide. Leave empty until the roof is separated from the walls.")]
    public Renderer[] roofRenderers = new Renderer[0];
    readonly Dictionary<Renderer, bool> previous = new Dictionary<Renderer, bool>();
    bool inside;

    public bool Contains(Vector3 worldPosition, float padding = 0f)
    {
        Vector3 p = transform.InverseTransformPoint(worldPosition) - areaCenter;
        Vector3 half = new Vector3(Mathf.Abs(areaSize.x), Mathf.Abs(areaSize.y), Mathf.Abs(areaSize.z)) * 0.5f;
        half += Vector3.one * Mathf.Max(0f, padding);
        return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
    }

    void LateUpdate()
    {
        if (!target)
        {
            var camera = Camera.main;
            var reveal = camera ? camera.GetComponent<CharacterRevealCamera>() : null;
            if (reveal) target = reveal.target;
        }
        bool shouldHide = target && Contains(target.position, inside ? exitPadding : 0f);
        if (shouldHide == inside) return;
        inside = shouldHide;
        if (!inside) { Restore(); return; }
        foreach (var roof in roofRenderers)
        {
            if (!roof || previous.ContainsKey(roof)) continue;
            previous.Add(roof, roof.forceRenderingOff);
            roof.forceRenderingOff = true;
        }
    }

    void Restore()
    {
        foreach (var pair in previous)
            if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
        previous.Clear();
        inside = false;
    }

    void OnDisable() => Restore();
    void OnDrawGizmosSelected()
    {
        var old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.85f);
        Gizmos.DrawWireCube(areaCenter, areaSize);
        Gizmos.matrix = old;
    }
}
