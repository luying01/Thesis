using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A segmented displacement track drawn along an object's real path of motion.
///
/// Design rules baked in here (do not "improve" these without re-testing):
///   - one segment = 5 cm, with a ~15% gap between segments
///   - single colour #185FA5, no alternating colours (Round A: no effect)
///   - no direction arrowheads (direction is felt directly in VR, and the
///     arrow form is already taken by the force aid - one form, two meanings
///     would confuse)
///   - no numeric labels anywhere (the track grows in real time, so any
///     hard-coded count is wrong in every intermediate state)
///   - a short tick marks the start point
///   - the last segment is drawn partially, in true proportion
///   - each track is anchored to its own path; tracks are never aligned to
///     each other (the two objects move in opposite directions and start at
///     different heights)
/// </summary>
public class SegmentedTrackVisual
{
    public GameObject root;

    private List<LineRenderer> segments = new List<LineRenderer>();
    private LineRenderer startTick;

    private Color baseColor;
    private float width;
    private float segmentLength;
    private float gapRatio;
    private int maxSegments;
    private float tickLength;
    private Vector3 planeNormal;
    private bool renderOnTop = false;

    private Vector3 origin = Vector3.zero;
    private Vector3 growDirection = Vector3.up;
    private Vector3 lateralOffset = Vector3.zero;
    private bool active = false;

    public bool IsActive { get { return active; } }

    public SegmentedTrackVisual(Transform parent, string name, Color color,
        float lineWidth, float segmentLengthMeters, float gapFraction,
        int maxSegmentCount, float startTickLength, Vector3 arrowPlaneNormal,
        bool drawOnTop)
    {
        renderOnTop = drawOnTop;
        baseColor = color;
        width = lineWidth;
        segmentLength = Mathf.Max(0.005f, segmentLengthMeters);
        gapRatio = Mathf.Clamp01(gapFraction);
        maxSegments = Mathf.Max(1, maxSegmentCount);
        tickLength = startTickLength;
        planeNormal = arrowPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.back : arrowPlaneNormal.normalized;

        root = new GameObject(name);
        root.transform.SetParent(parent, false);

        startTick = CreateLine(root.transform, "StartTick");

        for (int i = 0; i < maxSegments; i++)
            segments.Add(CreateLine(root.transform, "Segment_" + i));

        root.SetActive(false);
    }

    private LineRenderer CreateLine(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Material mat = new Material(Shader.Find("Sprites/Default"));
        if (renderOnTop)
        {
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000;
        }
        lr.material = mat;
        lr.startColor = baseColor;
        lr.endColor = baseColor;
        lr.gameObject.SetActive(false);
        return lr;
    }

    /// <summary>
    /// Start a new track. originPoint is the object's position at the moment
    /// tracking began - for the free rope end this is the rope end's own
    /// starting position, NOT the hand position.
    /// </summary>
    public void Begin(Vector3 originPoint, Vector3 lateral)
    {
        origin = originPoint;
        lateralOffset = lateral;
        growDirection = Vector3.up;
        active = true;
        root.SetActive(true);

        Vector3 basePoint = origin + lateralOffset;
        Vector3 side = Vector3.Cross(Vector3.up, planeNormal);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        side = side.normalized;

        startTick.gameObject.SetActive(true);
        startTick.SetPosition(0, basePoint - side * tickLength * 0.5f);
        startTick.SetPosition(1, basePoint + side * tickLength * 0.5f);

        for (int i = 0; i < segments.Count; i++)
            segments[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Update the drawn length. signedDisplacement is the displacement along
    /// the motion-constraint direction (vertical here); its sign sets the
    /// growth direction, its magnitude sets the length.
    /// </summary>
    public void UpdateLength(float signedDisplacement)
    {
        if (!active) return;

        growDirection = signedDisplacement >= 0f ? Vector3.up : Vector3.down;
        float distance = Mathf.Abs(signedDisplacement);

        Vector3 basePoint = origin + lateralOffset;
        float drawn = segmentLength * (1f - gapRatio);

        for (int i = 0; i < segments.Count; i++)
        {
            float segStart = i * segmentLength;
            float remaining = distance - segStart;

            if (remaining <= 0f)
            {
                segments[i].gameObject.SetActive(false);
                continue;
            }

            // Partial trailing segment: draw it in true proportion.
            float segDrawn = Mathf.Min(remaining, drawn);
            if (segDrawn < 0.0008f)
            {
                segments[i].gameObject.SetActive(false);
                continue;
            }

            Vector3 a = basePoint + growDirection * segStart;
            Vector3 b = a + growDirection * segDrawn;

            segments[i].gameObject.SetActive(true);
            segments[i].SetPosition(0, a);
            segments[i].SetPosition(1, b);
        }
    }

    public void SetAlpha(float a)
    {
        Color c = baseColor;
        c.a = Mathf.Clamp01(a);
        if (startTick != null) { startTick.startColor = c; startTick.endColor = c; }
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].startColor = c;
            segments[i].endColor = c;
        }
    }

    public void Clear()
    {
        active = false;
        if (root != null) root.SetActive(false);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
        segments.Clear();
    }
}