using UnityEngine;
using TMPro;

/// <summary>
/// The fixed formula panel shown for velocity questions (1-2 and 3-2).
///
/// Pure symbols only - no numbers anywhere. The chained equality itself
/// expresses "these three ratios are the same", so no explanatory sentence is
/// added. k is deliberately left undetermined: the student has to read it off
/// the segment counts on the displacement tracks.
///
///   1-2  shows up to the v row only.
///   3-2  shows up to the v row first, then expands the a row on request.
///
/// Each tracked object carries a small marker tag ([1] = load side,
/// [2] = free rope end) and the matching rows of the panel use the same tag,
/// so the panel can be mapped back onto the two tracks.
/// </summary>
public class FormulaPanel
{
    public GameObject root;

    private TextMeshPro text;
    private TextMeshPro markerLoad;
    private TextMeshPro markerFree;

    private string tagLoad = "[1]";
    private string tagFree = "[2]";
    private string subscriptLoad = "_load";
    private string subscriptFree = "_free";
    private Vector3 planeNormal = Vector3.back;

    private bool showMarkers = true;
    private bool renderOnTop = false;

    private bool accelerationExpanded = false;
    public bool AccelerationExpanded { get { return accelerationExpanded; } }

    private bool canExpand = false;
    public bool CanExpand { get { return canExpand && !accelerationExpanded; } }

    public FormulaPanel(Transform parent, string name, float fontSize,
        float markerFontSize, Color color, string loadTag, string freeTag,
        Vector3 arrowPlaneNormal, bool displayMarkers, bool drawOnTop,
        float panelWidth, float panelHeight,
        string loadSubscript, string freeSubscript)
    {
        subscriptLoad = loadSubscript;
        subscriptFree = freeSubscript;
        showMarkers = displayMarkers;
        renderOnTop = drawOnTop;
        tagLoad = loadTag;
        tagFree = freeTag;
        planeNormal = arrowPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.back : arrowPlaneNormal.normalized;

        root = new GameObject(name);
        root.transform.SetParent(parent, false);

        GameObject textObj = new GameObject("PanelText");
        textObj.transform.SetParent(root.transform, false);
        text = textObj.AddComponent<TextMeshPro>();
        text.fontSize = fontSize;
        text.color = color;
        text.faceColor = color;
        text.alignment = TextAlignmentOptions.TopLeft;

        // A procedurally created TextMeshPro gets a tiny default RectTransform.
        // A single glyph survives that, but five lines of formula get wrapped
        // to nothing and clipped, which is why the arrow labels appeared and
        // this panel did not. Give it a real rect and let it overflow.
        RectTransform rt = text.rectTransform;
        rt.sizeDelta = new Vector2(panelWidth, panelHeight);
        rt.pivot = new Vector2(0f, 1f);
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.text = "";

        markerLoad = CreateMarker("Marker_Load", markerFontSize, color);
        markerFree = CreateMarker("Marker_Free", markerFontSize, color);
        markerLoad.gameObject.SetActive(showMarkers);
        markerFree.gameObject.SetActive(showMarkers);

        if (renderOnTop)
        {
            text.fontMaterial.SetFloat("_ZTestMode",
                (float)UnityEngine.Rendering.CompareFunction.Always);
            text.fontMaterial.renderQueue = 4001;
        }

        root.SetActive(false);
    }

    private TextMeshPro CreateMarker(string name, float fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root.transform, false);
        TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.faceColor = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(fontSize * 2f, fontSize);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(255, 255, 255, 255);
        return tmp;
    }

    /// <summary>
    /// Show the panel. allowAccelerationRow is true only for 3-2, where the
    /// a row is revealed in a second step.
    /// </summary>
    public void Show(bool allowAccelerationRow)
    {
        canExpand = allowAccelerationRow;
        accelerationExpanded = false;
        root.SetActive(true);
        markerLoad.gameObject.SetActive(showMarkers);
        markerFree.gameObject.SetActive(showMarkers);
        markerLoad.text = tagLoad;
        markerFree.text = tagFree;
        RebuildText();
    }

    /// <summary>Reveal the acceleration row (3-2 only, second step).</summary>
    public bool ExpandAcceleration()
    {
        if (!canExpand || accelerationExpanded) return false;
        accelerationExpanded = true;
        RebuildText();
        return true;
    }

    // ©¤©¤ Text layout ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private static string Pad(int n)
    {
        return n <= 0 ? "" : new string(' ', n);
    }

    /// <summary>Centre a cell's content inside a fixed column width.</summary>
    private static string Centre(string text, int width)
    {
        int slack = width - text.Length;
        if (slack <= 0) return text;
        int left = slack / 2;
        return Pad(left) + text + Pad(slack - left);
    }

    /// <summary>
    /// Append one stacked fraction to a three-line row: numerator on top,
    /// a rule in the middle, denominator underneath. Stacked reads far better
    /// than inline "a / b" once several ratios are chained together.
    /// </summary>
    private static void AppendFraction(
        System.Text.StringBuilder top,
        System.Text.StringBuilder mid,
        System.Text.StringBuilder bot,
        string prefix, string numerator, string denominator)
    {
        int width = Mathf.Max(numerator.Length, denominator.Length);
        top.Append(Pad(prefix.Length)).Append(Centre(numerator, width));
        mid.Append(prefix).Append(new string('\u2500', width));
        bot.Append(Pad(prefix.Length)).Append(Centre(denominator, width));
    }

    private void AppendRow(System.Text.StringBuilder body,
        string label, string[,] cells, int cellCount)
    {
        var top = new System.Text.StringBuilder();
        var mid = new System.Text.StringBuilder();
        var bot = new System.Text.StringBuilder();

        for (int i = 0; i < cellCount; i++)
        {
            string prefix = i == 0 ? label : "  =  ";
            AppendFraction(top, mid, bot, prefix, cells[i, 0], cells[i, 1]);
        }

        body.Append(top).Append('\n');
        body.Append(mid).Append('\n');
        body.Append(bot).Append('\n');
    }

    private void RebuildText()
    {
        var body = new System.Text.StringBuilder();

        // Definitions, stacked the same way as the ratio row so the whole
        // panel reads with one visual grammar.
        string[,] vDef = { { "\u0394s", "\u0394t" } };
        AppendRow(body, "v = ", vDef, 1);

        if (accelerationExpanded)
        {
            string[,] aDef = { { "\u0394v", "\u0394t" } };
            AppendRow(body, "a = ", aDef, 1);
        }

        body.Append(new string('\u2500', 26)).Append('\n');

        // k is deliberately left undetermined: the student reads it off the
        // segment counts on the two displacement tracks.
        string nFree = subscriptFree;
        string nLoad = subscriptLoad;

        string[,] kCells = new string[3, 2];
        kCells[0, 0] = "\u0394s" + nFree; kCells[0, 1] = "\u0394s" + nLoad;
        kCells[1, 0] = "v" + nFree; kCells[1, 1] = "v" + nLoad;
        kCells[2, 0] = "a" + nFree; kCells[2, 1] = "a" + nLoad;

        AppendRow(body, "k = ", kCells, accelerationExpanded ? 3 : 2);

        text.text = "<mspace=0.58em>" + body.ToString().TrimEnd('\n') + "</mspace>";
    }

    /// <summary>
    /// Place and orient the panel. faceDirection is the direction the text
    /// should look along - i.e. from the panel toward the viewer, since a
    /// TextMeshPro mesh reads correctly when its local +Z points at the reader.
    /// </summary>
    public void SetPosition(Vector3 worldPosition, Vector3 faceDirection,
        Vector3 rotationOffsetEuler)
    {
        Vector3 face = faceDirection.sqrMagnitude < 0.0001f ? planeNormal : faceDirection.normalized;
        root.transform.position = worldPosition;
        text.transform.position = worldPosition;
        // Facing first, then the manual offset on top, so tilting the panel
        // does not undo the "must point at the reader" part.
        text.transform.rotation = Quaternion.LookRotation(face, Vector3.up)
                                  * Quaternion.Euler(rotationOffsetEuler);
    }

    /// <summary>
    /// Font size has to be re-applied, not just set once in the constructor,
    /// or tuning the Inspector value during Play does nothing.
    /// </summary>
    public void SetFontSize(float fontSize, float markerFontSize,
        float panelWidth, float panelHeight)
    {
        if (text != null && !Mathf.Approximately(text.fontSize, fontSize))
        {
            text.fontSize = fontSize;
            text.rectTransform.sizeDelta = new Vector2(panelWidth, panelHeight);
            text.ForceMeshUpdate();
        }
        if (markerLoad != null) markerLoad.fontSize = markerFontSize;
        if (markerFree != null) markerFree.fontSize = markerFontSize;
    }

    /// <summary>Place the two object marker tags next to their objects.</summary>
    public void SetMarkerPositions(Vector3 loadPosition, Vector3 freePosition,
        Vector3 faceDirection, Vector3 rotationOffsetEuler)
    {
        if (!showMarkers) return;
        Vector3 face = faceDirection.sqrMagnitude < 0.0001f ? planeNormal : faceDirection.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up)
                         * Quaternion.Euler(rotationOffsetEuler);
        markerLoad.transform.position = loadPosition;
        markerLoad.transform.rotation = rot;
        markerFree.transform.position = freePosition;
        markerFree.transform.rotation = rot;
    }

    public void SetAlpha(float a)
    {
        float clamped = Mathf.Clamp01(a);
        Color c;

        c = text.color; c.a = clamped; text.color = c;
        c = markerLoad.color; c.a = clamped; markerLoad.color = c;
        c = markerFree.color; c.a = clamped; markerFree.color = c;
    }

    /// <summary>Diagnostic: what the panel currently thinks it is rendering.</summary>
    public string DescribeState()
    {
        if (root == null) return "root=null";
        return "active=" + root.activeSelf
             + " chars=" + (text != null ? text.text.Length : 0)
             + " bounds=" + (text != null ? text.textBounds.size.ToString("F3") : "n/a")
             + " rect=" + (text != null ? text.rectTransform.sizeDelta.ToString("F2") : "n/a");
    }

    public void Hide()
    {
        canExpand = false;
        accelerationExpanded = false;
        if (root != null) root.SetActive(false);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}