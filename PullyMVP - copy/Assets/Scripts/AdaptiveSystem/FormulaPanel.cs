using UnityEngine;
using TMPro;

/// <summary>
/// The formula panel for velocity questions (1-2 and 3-2).
///
/// Three states, cycled by a small button on the panel itself:
///
///   Collapsed - just the button. This is the default, because Round A
///               feedback was that a long, not-yet-understood formula left
///               permanently on screen ADDS cognitive load rather than
///               relieving it. The aid has to be askable-for, not imposed.
///   Basic     - the definition of v and the two-column ratio.
///   Expanded  - adds the a row and the third ratio column. Movable-pulley
///               questions only.
///
/// Pure symbols, no numbers: k is deliberately left undetermined so the
/// student reads it off the segment counts on the displacement tracks.
/// </summary>
public class FormulaPanel
{
    public enum PanelState { Collapsed, Basic, Expanded }

    public GameObject root;

    private RoundedPanelVisual background;
    private RoundedPanelVisual buttonBackground;
    private TextMeshPro text;
    private TextMeshPro buttonLabel;
    private TextMeshPro markerLoad;
    private TextMeshPro markerFree;
    private TouchButton touchButton;

    private PanelStyle style;
    private string tagLoad = "[1]";
    private string tagFree = "[2]";
    private string subscriptLoad = "_load";
    private string subscriptFree = "_free";
    private Vector3 planeNormal = Vector3.back;
    private bool showMarkers = true;

    private PanelState state = PanelState.Collapsed;
    private bool allowExpanded = false;
    private float alpha = 1f;

    private string labelCollapsed = "Formula";
    private string labelBasic = "More";
    private string labelExpanded = "Hide";

    public PanelState State { get { return state; } }
    public bool AccelerationExpanded { get { return state == PanelState.Expanded; } }
    public bool CanExpand { get { return allowExpanded && state == PanelState.Basic; } }

    public FormulaPanel(Transform parent, string name, float fontSize,
        float markerFontSize, Color color, string loadTag, string freeTag,
        Vector3 arrowPlaneNormal, bool displayMarkers, bool drawOnTop,
        float panelWidth, float panelHeight,
        string loadSubscript, string freeSubscript,
        PanelStyle panelStyle, int uiLayer,
        string collapsedLabel, string basicLabel, string expandedLabel)
    {
        style = panelStyle != null ? panelStyle : ScriptableObject.CreateInstance<PanelStyle>();
        tagLoad = loadTag;
        tagFree = freeTag;
        subscriptLoad = loadSubscript;
        subscriptFree = freeSubscript;
        showMarkers = displayMarkers;
        planeNormal = arrowPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.back : arrowPlaneNormal.normalized;
        labelCollapsed = collapsedLabel;
        labelBasic = basicLabel;
        labelExpanded = expandedLabel;

        root = new GameObject(name);
        root.transform.SetParent(parent, false);

        background = new RoundedPanelVisual(root.transform, "PanelBackground", style, drawOnTop);

        GameObject textObj = new GameObject("PanelText");
        textObj.transform.SetParent(root.transform, false);
        text = textObj.AddComponent<TextMeshPro>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false;
        text.rectTransform.sizeDelta = new Vector2(panelWidth, panelHeight);
        text.rectTransform.pivot = new Vector2(0f, 1f);

        // The toggle: its own little rounded pill with a collider the quiz ray
        // can hit, so the student can dismiss or summon the formula at will.
        buttonBackground = new RoundedPanelVisual(root.transform, "ToggleBackground", style, drawOnTop);

        GameObject btnObj = new GameObject("ToggleButton");
        btnObj.transform.SetParent(root.transform, false);
        if (uiLayer >= 0 && uiLayer < 32) btnObj.layer = uiLayer;

        buttonLabel = btnObj.AddComponent<TextMeshPro>();
        buttonLabel.fontSize = markerFontSize;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.overflowMode = TextOverflowModes.Overflow;
        buttonLabel.rectTransform.sizeDelta = new Vector2(panelWidth * 0.5f, panelHeight * 0.3f);

        // Size the collider before TouchButton is added: TouchButton only
        // overwrites it when a RectTransform reports a rect, and a bare
        // TextMeshPro's rect is not what we want the hit area to be.
        BoxCollider col = btnObj.AddComponent<BoxCollider>();
        col.isTrigger = false;
        col.size = new Vector3(0.10f, 0.05f, 0.01f);

        touchButton = btnObj.AddComponent<TouchButton>();
        touchButton.cooldownTime = 0.3f;
        // A UnityEvent field is only instantiated by Unity's deserialiser, so a
        // component added at runtime has a null event until something assigns
        // one. AddListener on that null is what threw in the constructor.
        if (touchButton.onTouched == null)
            touchButton.onTouched = new UnityEngine.Events.UnityEvent();
        touchButton.onTouched.AddListener(CycleState);

        markerLoad = CreateMarker("Marker_Load", markerFontSize, color);
        markerFree = CreateMarker("Marker_Free", markerFontSize, color);
        markerLoad.gameObject.SetActive(showMarkers);
        markerFree.gameObject.SetActive(showMarkers);

        if (drawOnTop)
        {
            SetOnTop(text);
            SetOnTop(buttonLabel);
        }


        ApplyStyleToText();
        root.SetActive(false);
    }

    private static void SetOnTop(TextMeshPro tmp)
    {
        if (tmp == null || tmp.fontMaterial == null) return;
        tmp.fontMaterial.SetFloat("_ZTestMode",
            (float)UnityEngine.Rendering.CompareFunction.Always);
        tmp.fontMaterial.renderQueue = 4001;
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
        return tmp;
    }

    private void ApplyStyleToText()
    {
        if (style == null) return;

        text.color = style.textColor;
        text.faceColor = style.textColor;
        text.outlineColor = style.textOutlineColor;
        text.outlineWidth = style.textOutlineWidth;

        buttonLabel.color = style.textColor;
        buttonLabel.faceColor = style.textColor;
        buttonLabel.outlineColor = style.textOutlineColor;
        buttonLabel.outlineWidth = style.textOutlineWidth;
    }

    // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Bring the panel up for a question. allowAcceleration is true only for
    /// movable-pulley velocity questions (3-2).
    /// </summary>
    public void Show(bool allowAcceleration, PanelState initialState)
    {
        allowExpanded = allowAcceleration;
        state = initialState;
        if (state == PanelState.Expanded && !allowExpanded) state = PanelState.Basic;

        root.SetActive(true);
        markerLoad.gameObject.SetActive(showMarkers && state != PanelState.Collapsed);
        markerFree.gameObject.SetActive(showMarkers && state != PanelState.Collapsed);

        RebuildText();
    }

    /// <summary>Collapsed to Basic to Expanded and back round again.</summary>
    public void CycleState()
    {
        switch (state)
        {
            case PanelState.Collapsed:
                state = PanelState.Basic;
                break;
            case PanelState.Basic:
                state = allowExpanded ? PanelState.Expanded : PanelState.Collapsed;
                break;
            default:
                state = PanelState.Collapsed;
                break;
        }
        RebuildText();
    }

    public bool ExpandAcceleration()
    {
        if (!allowExpanded || state == PanelState.Expanded) return false;
        state = PanelState.Expanded;
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
        buttonLabel.text = state == PanelState.Collapsed ? labelCollapsed
                         : (state == PanelState.Basic && allowExpanded) ? labelBasic
                         : labelExpanded;

        if (state == PanelState.Collapsed)
        {
            text.text = "";
            if (markerLoad != null) markerLoad.gameObject.SetActive(false);
            if (markerFree != null) markerFree.gameObject.SetActive(false);
            return;
        }

        if (showMarkers)
        {
            markerLoad.gameObject.SetActive(true);
            markerFree.gameObject.SetActive(true);
            markerLoad.text = tagLoad;
            markerFree.text = tagFree;
        }

        var body = new System.Text.StringBuilder();

        // Definitions, stacked the same way as the ratio row so the whole
        // panel reads with one visual grammar.
        string[,] vDef = { { "\u0394s", "\u0394t" } };
        AppendRow(body, "v = ", vDef, 1);

        if (state == PanelState.Expanded)
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

        AppendRow(body, "k = ", kCells, state == PanelState.Expanded ? 3 : 2);

        text.text = "<mspace=0.58em>" + body.ToString().TrimEnd('\n') + "</mspace>";
    }


    // ©¤©¤ Placement ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Place and orient the panel. faceDirection points from the panel toward
    /// the reader: a TextMeshPro mesh only reads correctly when its local +Z
    /// points at whoever is looking at it.
    /// </summary>
    public void SetPosition(Vector3 worldPosition, Vector3 faceDirection,
        Vector3 rotationOffsetEuler)
    {
        Vector3 face = faceDirection.sqrMagnitude < 0.0001f ? planeNormal : faceDirection.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up)
                         * Quaternion.Euler(rotationOffsetEuler);

        root.transform.position = worldPosition;
        root.transform.rotation = rot;

        text.transform.position = worldPosition;
        text.transform.rotation = rot;

        LayoutBackgrounds(worldPosition, rot);
    }

    /// <summary>
    /// Fit the backdrop to whatever the text currently measures, and park the
    /// toggle just under it. Done after the text has been laid out so the panel
    /// never clips its own contents.
    /// </summary>
    private void LayoutBackgrounds(Vector3 worldPosition, Quaternion rot)
    {
        if (style == null) return;

        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;
        Vector3 back = rot * Vector3.forward * -0.002f; // sit just behind the text

        float btnW = 0.11f;
        float btnH = 0.045f;

        if (state == PanelState.Collapsed)
        {
            background.SetVisible(false);
            text.gameObject.SetActive(false);

            buttonBackground.SetVisible(true);
            buttonBackground.SetSize(btnW, btnH);
            buttonBackground.SetPose(worldPosition + back, rot);
            buttonLabel.transform.position = worldPosition;
            buttonLabel.transform.rotation = rot;
            return;
        }

        text.gameObject.SetActive(true);
        text.ForceMeshUpdate();

        Vector2 bounds = text.textBounds.size;
        float w = Mathf.Max(0.05f, bounds.x + style.paddingX * 2f);
        float h = Mathf.Max(0.04f, bounds.y + style.paddingY * 2f);

        // The text pivot is top-left, so the backdrop centre sits half a panel
        // right of and half a panel below the anchor point.
        Vector3 centre = worldPosition + right * (w * 0.5f) - up * (h * 0.5f);

        background.SetVisible(true);
        background.SetSize(w, h);
        background.SetPose(centre + back, rot);

        Vector3 btnCentre = centre - up * (h * 0.5f + btnH * 0.6f);
        buttonBackground.SetVisible(true);
        buttonBackground.SetSize(btnW, btnH);
        buttonBackground.SetPose(btnCentre + back, rot);
        buttonLabel.transform.position = btnCentre;
        buttonLabel.transform.rotation = rot;
    }

    public void SetMarkerPositions(Vector3 loadPosition, Vector3 freePosition,
        Vector3 faceDirection, Vector3 rotationOffsetEuler)
    {
        if (!showMarkers || state == PanelState.Collapsed) return;
        Vector3 face = faceDirection.sqrMagnitude < 0.0001f ? planeNormal : faceDirection.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up)
                         * Quaternion.Euler(rotationOffsetEuler);
        markerLoad.transform.position = loadPosition;
        markerLoad.transform.rotation = rot;
        markerFree.transform.position = freePosition;
        markerFree.transform.rotation = rot;
    }

    /// <summary>
    /// Font size and style have to be re-applied, not just set once in the
    /// constructor, or tuning them in the Inspector during Play does nothing.
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
        if (buttonLabel != null) buttonLabel.fontSize = markerFontSize;
        if (markerLoad != null) markerLoad.fontSize = markerFontSize;
        if (markerFree != null) markerFree.fontSize = markerFontSize;

        ApplyStyleToText();
        if (background != null) background.Apply();
        if (buttonBackground != null) buttonBackground.Apply();
    }

    public void SetAlpha(float a)
    {
        alpha = Mathf.Clamp01(a);

        Color c = style != null ? style.textColor : Color.white;
        c.a = alpha;
        text.color = c;
        buttonLabel.color = c;

        Color m = markerLoad.color; m.a = alpha; markerLoad.color = m;
        m = markerFree.color; m.a = alpha; markerFree.color = m;

        if (background != null) background.SetAlpha(alpha);
        if (buttonBackground != null) buttonBackground.SetAlpha(alpha);
    }

    /// <summary>Diagnostic: what the panel currently thinks it is rendering.</summary>
    public string DescribeState()
    {
        if (root == null) return "root=null";
        return "active=" + root.activeSelf
             + " state=" + state
             + " chars=" + (text != null ? text.text.Length : 0)
             + " bounds=" + (text != null ? text.textBounds.size.ToString("F3") : "n/a");
    }

    public void Hide()
    {
        allowExpanded = false;
        state = PanelState.Collapsed;
        if (root != null) root.SetActive(false);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}