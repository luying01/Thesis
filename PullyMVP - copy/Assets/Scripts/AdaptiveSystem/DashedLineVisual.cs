using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A thin dashed line, built from a pool of short LineRenderer segments.
///
/// Used for the permanent leader lines that connect the merged force stack
/// back to the rope segments the arrows originally came from. Without these
/// the stack becomes untraceable once the merge animation has finished.
/// </summary>
public class DashedLineVisual
{
    public GameObject root;

    private List<LineRenderer> dashes = new List<LineRenderer>();
    private Color baseColor;
    private float width;
    private float dashLength;
    private float gapLength;
    private int maxDashes;
    private bool renderOnTop = false;

    public DashedLineVisual(Transform parent, string name, Color color,
        float lineWidth, float dashLen, float gapLen, int maxDashCount,
        bool drawOnTop)
    {
        renderOnTop = drawOnTop;
        baseColor = color;
        width = lineWidth;
        dashLength = Mathf.Max(0.001f, dashLen);
        gapLength = Mathf.Max(0.001f, gapLen);
        maxDashes = Mathf.Max(2, maxDashCount);

        root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.SetActive(false);
    }

    private LineRenderer CreateDash(int index)
    {
        GameObject obj = new GameObject("Dash_" + index);
        obj.transform.SetParent(root.transform, false);
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 0;
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
        return lr;
    }

    /// <summary>Draw a dashed line between two world points.</summary>
    public void SetLine(Vector3 from, Vector3 to)
    {
        root.SetActive(true);

        Vector3 delta = to - from;
        float total = delta.magnitude;
        if (total < 0.0005f)
        {
            HideAllDashes();
            return;
        }

        Vector3 dir = delta / total;
        float step = dashLength + gapLength;
        int needed = Mathf.Min(maxDashes, Mathf.CeilToInt(total / step));

        while (dashes.Count < needed)
            dashes.Add(CreateDash(dashes.Count));

        for (int i = 0; i < dashes.Count; i++)
        {
            if (i < needed)
            {
                float a = i * step;
                float b = Mathf.Min(a + dashLength, total);
                dashes[i].gameObject.SetActive(true);
                dashes[i].SetPosition(0, from + dir * a);
                dashes[i].SetPosition(1, from + dir * b);
            }
            else
            {
                dashes[i].gameObject.SetActive(false);
            }
        }
    }

    private void HideAllDashes()
    {
        for (int i = 0; i < dashes.Count; i++)
            dashes[i].gameObject.SetActive(false);
    }

    public void SetAlpha(float a)
    {
        Color c = baseColor;
        c.a = Mathf.Clamp01(a);
        for (int i = 0; i < dashes.Count; i++)
        {
            dashes[i].startColor = c;
            dashes[i].endColor = c;
        }
    }

    public void SetVisible(bool v)
    {
        if (root != null) root.SetActive(v);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
        dashes.Clear();
    }
}