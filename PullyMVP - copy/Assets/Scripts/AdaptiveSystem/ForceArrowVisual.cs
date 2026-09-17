using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

/// <summary>
/// A single force arrow: shaft + two head strokes + a symbol label.
///
/// Built procedurally from LineRenderers, so nothing needs to be dragged into
/// the Inspector. The length of an arrow is set once, from the sampled force,
/// and is NEVER changed afterwards. Translate() moves the arrow without
/// touching its length - this is what the stage-2 merge animation uses.
/// </summary>
public class ForceArrowVisual
{
    public GameObject root;

    private LineRenderer shaft;
    private LineRenderer headLeft;
    private LineRenderer headRight;
    private TextMeshPro label;

    private Color baseColor;
    private float headSize;
    private float labelOffset;

    private Vector3 startPoint = Vector3.zero;
    private Vector3 direction = Vector3.up;
    private float length = 0f;
    private Vector3 planeNormal = Vector3.back;

    private float alpha = 1f;
    private bool visible = false;
    private bool renderOnTop = false;
    public int ComparisonSlot = 0;

    public float Length { get { return length; } }
    public Vector3 StartPoint { get { return startPoint; } }
    public Vector3 EndPoint { get { return startPoint + direction * length; } }
    public Vector3 Direction { get { return direction; } }
    public Vector3 MidPoint { get { return startPoint + direction * (length * 0.5f); } }

    public ForceArrowVisual(Transform parent, string name, Color color,
        float lineWidth, float arrowHeadSize, float labelFontSize,
        float labelSideOffset, Vector3 arrowPlaneNormal, bool drawOnTop)
    {
        renderOnTop = drawOnTop;
        baseColor = color;
        headSize = arrowHeadSize;
        labelOffset = labelSideOffset;
        planeNormal = arrowPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.back : arrowPlaneNormal.normalized;

        root = new GameObject(name);
        root.transform.SetParent(parent, false);

        shaft = CreateLine(root.transform, "Shaft", color, lineWidth);
        headLeft = CreateLine(root.transform, "HeadL", color, lineWidth);
        headRight = CreateLine(root.transform, "HeadR", color, lineWidth);

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(root.transform, false);
        label = labelObj.AddComponent<TextMeshPro>();
        label.fontSize = labelFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.text = "";
        label.enableVertexGradient = false;

        // Force BOTH the vertex colour and the material face colour. Setting
        // only .color leaves the font material's own face colour in place,
        // which is what made the symbols render in the material's default tint
        // instead of matching their arrow.
        label.color = color;
        label.faceColor = color;

        // White outline so the symbol stays readable against scene textures.
        // Kept thin: a heavy outline on a small glyph swallows the face colour.
        label.outlineColor = new Color32(255, 255, 255, 255);
        label.outlineWidth = 0.12f;

        if (renderOnTop)
        {
            label.fontMaterial.SetFloat("_ZTestMode", (float)CompareFunction.Always);
            label.fontMaterial.renderQueue = 4001;
        }

        SetVisible(false);
    }

    private LineRenderer CreateLine(Transform parent, string name, Color color, float width)
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
            // Draw through the rack, the pulleys and the rope, so the aid is
            // never hidden behind the apparatus it is describing.
            mat.SetInt("_ZTest", (int)CompareFunction.Always);
            mat.renderQueue = 4000;
        }
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;
        return lr;
    }

    /// <summary>
    /// Define the arrow. The length comes from the sampled force and is fixed
    /// from this point on.
    /// </summary>
    public void SetArrow(Vector3 start, Vector3 dir, float lengthMeters, string symbolText)
    {
        startPoint = start;
        direction = dir.sqrMagnitude < 0.0001f ? Vector3.up : dir.normalized;
        length = lengthMeters;
        if (label != null) label.text = symbolText;
        Rebuild();
    }

    /// <summary>Move the arrow without changing its length or direction.</summary>
    public void Translate(Vector3 delta)
    {
        startPoint += delta;
        Rebuild();
    }

    /// <summary>Move the arrow so that its start sits at a new point.</summary>
    public void SetStartPoint(Vector3 newStart)
    {
        startPoint = newStart;
        Rebuild();
    }

    public void Rebuild()
    {
        if (shaft == null) return;

        Vector3 end = startPoint + direction * length;

        shaft.SetPosition(0, startPoint);
        shaft.SetPosition(1, end);

        Vector3 side = Vector3.Cross(direction, planeNormal);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(direction, Vector3.forward);
        side = side.normalized;

        headLeft.SetPosition(0, end);
        headLeft.SetPosition(1, end - direction * headSize + side * headSize * 0.5f);
        headRight.SetPosition(0, end);
        headRight.SetPosition(1, end - direction * headSize - side * headSize * 0.5f);

        if (label != null)
        {
            label.transform.position = startPoint + direction * (length * 0.5f) + side * labelOffset;
            label.transform.rotation = Quaternion.LookRotation(planeNormal, Vector3.up);
        }
    }

    public void SetAlpha(float a)
    {
        alpha = Mathf.Clamp01(a);
        Color c = baseColor;
        c.a = alpha;
        if (shaft != null) { shaft.startColor = c; shaft.endColor = c; }
        if (headLeft != null) { headLeft.startColor = c; headLeft.endColor = c; }
        if (headRight != null) { headRight.startColor = c; headRight.endColor = c; }
        if (label != null)
        {
            Color lc = baseColor;
            lc.a = alpha;
            label.color = lc;
            label.faceColor = lc;
            label.outlineColor = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
        }
    }

    public void SetVisible(bool v)
    {
        visible = v;
        if (root != null) root.SetActive(v);
    }

    public bool IsVisible { get { return visible; } }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}