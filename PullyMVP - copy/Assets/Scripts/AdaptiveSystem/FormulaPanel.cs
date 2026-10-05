using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Every piece of wording on the formula panel, editable in the Inspector so
/// the text can be tuned without touching code.
///
/// {free} and {load} are replaced at runtime with a real subscript, e.g.
/// "x{free}" renders as x with a small "free" below the baseline.
/// Legend lines are split at the first " = " into a symbol column and a
/// description column so the descriptions line up.
/// </summary>
[System.Serializable]
public class FormulaPanelTexts
{
    [Header("Toggle button")]
    public string buttonShow = "Show formula";
    public string buttonMore = "More";
    public string buttonHide = "Hide";

    [Header("Subscripts")]
    public string subscriptFree = "free";
    public string subscriptLoad = "load";

    [Header("Basic: what each letter means (shown before the formulas)")]
    public string legendV = "v = velocity";
    public string legendA = "a = acceleration";
    public string legendDs = "Δs = change in position";
    public string legendDv = "Δv = change in velocity";
    public string legendDt = "Δt = change in time";

    [Header("Expanded: explanation shown after the k chain")]
    public string explainK = "k = a constant";
    public string explainFree = "x{free} = value at the free end of the rope";
    public string explainLoad = "x{load} = value at the load";
    [Tooltip("Relation sentence for the fixed-pulley question (1-2).")]
    [TextArea(2, 4)]
    public string relationFixed =
        "The change in position and the velocity of the free end are always k times those of the load.";
    [Tooltip("Relation sentence for the movable-pulley question (3-2).")]
    [TextArea(2, 4)]
    public string relationMovable =
        "The change in position, the velocity and the acceleration of the free end are always k times those of the load.";
}

/// <summary>
/// The formula panel for the velocity questions (1-2 and 3-2).
///
/// Three states, cycled by a small button that stays in the same place:
///
///   Collapsed - only the "Show formula" button. Default, because Round A
///               showed a permanently visible formula adds cognitive load.
///   Basic     - legend (what each letter means), then the definition(s):
///               1-2: v = ds/dt          3-2: v = ds/dt and a = dv/dt
///   Expanded  - Basic + the k chain, followed by its text explanation:
///               1-2: k = ds ratio = v ratio
///               3-2: k = ds ratio = v ratio = a ratio
///
/// Layout is built from separate text pieces and thin bars, so stacked
/// fractions line up regardless of font. All sizes are in metres; the panel
/// corner radius is a fixed physical size and the panel has no border.
///
/// Local frame: origin = top-left of the toggle button. The body opens above
/// the button by default (see SetOpensUpward), so the button never moves and
/// the panel never sinks below the table. Text is in the local XY plane;
/// backdrops sit slightly towards local +Z (behind the text).
/// </summary>
public class FormulaPanel
{
    public enum PanelState { Collapsed, Basic, Expanded }

    public GameObject root;

    // ── Settings ──────────────────────────────────────────────────────────

    private PanelStyle sharedStyle;
    private FormulaPanelTexts texts;
    private float fontSize = 0.2f;
    private float explanationScale = 0.75f;
    private float buttonScale = 0.9f;
    private float cornerRadius = 0.012f;
    private bool drawOnTop;
    private bool showMarkers;
    private Vector3 planeNormal = Vector3.back;
    private string tagLoad = "[1]";
    private string tagFree = "[2]";

    // ── State ─────────────────────────────────────────────────────────────

    private PanelState state = PanelState.Collapsed;
    private bool movablePulley = false;
    private float alpha = 1f;
    private bool layoutDirty = true;
    private bool opensUpward = true;

    public PanelState State { get { return state; } }
    public bool AccelerationExpanded { get { return movablePulley && state == PanelState.Expanded; } }
    public bool CanExpand { get { return state == PanelState.Basic; } }

    // ── Visual pieces ─────────────────────────────────────────────────────

    // Private style copies: the panel needs a per-size corner radius and no
    // border, without changing the shared asset that the warning HUD uses.
    private PanelStyle bodyStyle;
    private PanelStyle buttonStyle;
    private PanelStyle barStyle;

    private RoundedPanelVisual body;
    // Collider over the body on the quiz-ray layer, with NO TouchButton:
    // the ray becomes visible while reading, but nothing happens on trigger.
    private BoxCollider bodyHitArea;
    private RoundedPanelVisual buttonBackground;
    private TextMeshPro buttonLabel;
    private BoxCollider buttonCollider;
    private TouchButton touchButton;
    private TextMeshPro markerLoad;
    private TextMeshPro markerFree;

    private readonly List<TextMeshPro> textPool = new List<TextMeshPro>();
    private readonly List<RoundedPanelVisual> barPool = new List<RoundedPanelVisual>();
    private int textUsed;
    private int barUsed;

    private Vector2 buttonSize;
    private Vector2 bodySize;

    private const float ZBody = 0.002f;
    private const float ZBar = 0.001f;
    private const float ButtonGap = 0.008f;

    // Render order. UGUI world-space canvases (the quiz paper) draw at 3000.
    // The panel draws just BEFORE them, so the quiz paper always covers it,
    // while ZTest Always (drawOnTop) still keeps it visible through the
    // table and the apparatus. The panel writes no depth, so whatever draws
    // after it wins.
    private int queueBase = 2900;
    private int QueueBase { get { return queueBase; } }

    // ── Construction ──────────────────────────────────────────────────────

    public FormulaPanel(Transform parent, string name,
        float formulaFontSize, float explanationFontScale, float buttonFontScale,
        float markerFontSize, Color markerColor, string loadTag, string freeTag,
        Vector3 arrowPlaneNormal, bool displayMarkers, bool renderOnTop,
        PanelStyle panelStyle, string uiLayerName, float cornerRadiusMeters,
        FormulaPanelTexts panelTexts, int renderQueue)
    {
        queueBase = renderQueue;
        sharedStyle = panelStyle != null ? panelStyle : ScriptableObject.CreateInstance<PanelStyle>();
        texts = panelTexts ?? new FormulaPanelTexts();
        fontSize = Mathf.Max(0.001f, formulaFontSize);
        explanationScale = Mathf.Max(0.1f, explanationFontScale);
        buttonScale = Mathf.Max(0.1f, buttonFontScale);
        cornerRadius = Mathf.Max(0f, cornerRadiusMeters);
        drawOnTop = renderOnTop;
        showMarkers = displayMarkers;
        tagLoad = loadTag;
        tagFree = freeTag;
        planeNormal = arrowPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.back : arrowPlaneNormal.normalized;

        bodyStyle = MakeStyleCopy(sharedStyle.fillColor);
        buttonStyle = MakeStyleCopy(sharedStyle.fillColor);
        barStyle = MakeStyleCopy(sharedStyle.textColor);
        barStyle.cornerRadius = 0f;

        root = new GameObject(name);
        root.transform.SetParent(parent, false);

        body = new RoundedPanelVisual(root.transform, "PanelBackground", bodyStyle, drawOnTop);
        SetQueue(body, QueueBase);

        buttonBackground = new RoundedPanelVisual(root.transform, "ToggleBackground", buttonStyle, drawOnTop);
        SetQueue(buttonBackground, QueueBase);

        // ── Toggle button: label + collider + TouchButton on one object ──
        GameObject btnObj = new GameObject("ToggleButton");
        btnObj.transform.SetParent(root.transform, false);

        int layer = string.IsNullOrEmpty(uiLayerName) ? -1 : LayerMask.NameToLayer(uiLayerName);

        GameObject hitObj = new GameObject("BodyHitArea");
        hitObj.transform.SetParent(root.transform, false);
        if (layer >= 0) hitObj.layer = layer;
        bodyHitArea = hitObj.AddComponent<BoxCollider>();
        bodyHitArea.isTrigger = false;
        hitObj.SetActive(false);

        if (layer >= 0)
            btnObj.layer = layer;
        else
            Debug.LogWarning("[FormulaPanel] Layer '" + uiLayerName + "' not found. The toggle " +
                             "button will not be hit by the quiz ray. Check Formula UI Layer Name.");

        buttonLabel = btnObj.AddComponent<TextMeshPro>();
        ConfigureText(buttonLabel, TextAlignmentOptions.Center);

        buttonCollider = btnObj.AddComponent<BoxCollider>();
        buttonCollider.isTrigger = false;

        touchButton = btnObj.AddComponent<TouchButton>();
        touchButton.cooldownTime = 0.3f;
        // A UnityEvent on a component added at runtime starts out null.
        if (touchButton.onTouched == null)
            touchButton.onTouched = new UnityEngine.Events.UnityEvent();
        touchButton.onTouched.AddListener(CycleState);

        // ── Optional [1]/[2] markers next to the tracked objects ──
        markerLoad = CreateMarker("Marker_Load", markerFontSize, markerColor);
        markerFree = CreateMarker("Marker_Free", markerFontSize, markerColor);
        markerLoad.gameObject.SetActive(false);
        markerFree.gameObject.SetActive(false);

        root.SetActive(false);
    }

    private PanelStyle MakeStyleCopy(Color fill)
    {
        PanelStyle s = Object.Instantiate(sharedStyle);
        s.fillColor = fill;
        s.borderColor = fill;
        s.borderWidth = 0f;
        return s;
    }

    private static void SetQueue(RoundedPanelVisual v, int queue)
    {
        if (v == null || v.root == null) return;
        MeshRenderer mr = v.root.GetComponent<MeshRenderer>();
        if (mr != null && mr.sharedMaterial != null) mr.sharedMaterial.renderQueue = queue;
    }

    private void ConfigureText(TextMeshPro tmp, TextAlignmentOptions align)
    {
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableAutoSizing = false;
        tmp.richText = true;
        tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tmp.color = sharedStyle.textColor;
        tmp.faceColor = sharedStyle.textColor;
        tmp.outlineColor = sharedStyle.textOutlineColor;
        tmp.outlineWidth = sharedStyle.textOutlineWidth;

        if (tmp.fontMaterial != null)
        {
            // Text always draws after the backdrop and the fraction bars.
            tmp.fontMaterial.renderQueue = QueueBase + 2;
            if (drawOnTop)
                tmp.fontMaterial.SetFloat("_ZTestMode",
                    (float)UnityEngine.Rendering.CompareFunction.Always);
        }
    }

    private TextMeshPro CreateMarker(string name, float size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root.transform, false);
        TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
        tmp.fontSize = size;
        tmp.color = color;
        tmp.faceColor = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(size * 2f, size);
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    // ── State ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Bring the panel up for a question. isMovablePulley selects the 3-2
    /// content (adds acceleration); otherwise the 1-2 content is used.
    /// </summary>
    public void Show(bool isMovablePulley, PanelState initialState)
    {
        movablePulley = isMovablePulley;
        state = initialState;
        root.SetActive(true);
        layoutDirty = true;
        RebuildLayout();
    }

    /// <summary>Collapsed -> Basic -> Expanded -> Collapsed.</summary>
    public void CycleState()
    {
        switch (state)
        {
            case PanelState.Collapsed: state = PanelState.Basic; break;
            case PanelState.Basic: state = PanelState.Expanded; break;
            default: state = PanelState.Collapsed; break;
        }
        layoutDirty = true;
        RebuildLayout();
    }

    /// <summary>Jump straight to Expanded. Kept for API compatibility.</summary>
    public bool ExpandAcceleration()
    {
        if (state == PanelState.Expanded) return false;
        state = PanelState.Expanded;
        layoutDirty = true;
        RebuildLayout();
        return true;
    }

    public void Hide()
    {
        state = PanelState.Collapsed;
        if (root != null) root.SetActive(false);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }

    // ── Layout model ──────────────────────────────────────────────────────

    private enum BlockKind { Row, Legend, Paragraph, Divider, Spacer }

    private class Piece
    {
        public bool fraction;
        public TextMeshPro text;   // plain text piece
        public TextMeshPro num;    // fraction numerator
        public TextMeshPro den;    // fraction denominator
        public Vector2 size;       // plain text size
        public float width;
        public float above;        // extent above the row's axis
        public float below;        // extent below the row's axis
    }

    private class Block
    {
        public BlockKind kind;
        public List<Piece> pieces;
        public List<TextMeshPro> symbols;       // legend left column
        public List<TextMeshPro> descriptions;  // legend right column
        public TextMeshPro paragraph;
        public float width;
        public float height;
    }

    private string Sub(string token)
    {
        return "<sub>" + token + "</sub>";
    }

    private string Expand(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("{free}", Sub(texts.subscriptFree))
                .Replace("{load}", Sub(texts.subscriptLoad));
    }

    private TextMeshPro TakeText(string content, float size)
    {
        TextMeshPro t;
        if (textUsed < textPool.Count)
        {
            t = textPool[textUsed];
        }
        else
        {
            GameObject obj = new GameObject("Text_" + textPool.Count);
            obj.transform.SetParent(root.transform, false);
            t = obj.AddComponent<TextMeshPro>();
            ConfigureText(t, TextAlignmentOptions.Center);
            textPool.Add(t);
        }
        textUsed++;

        // Reset anything a previous use (e.g. the wrapped paragraph) changed.
        t.gameObject.SetActive(true);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = size;
        t.text = content;
        Vector2 pref = t.GetPreferredValues(content);
        t.rectTransform.sizeDelta = pref;
        return t;
    }

    private RoundedPanelVisual TakeBar()
    {
        RoundedPanelVisual b;
        if (barUsed < barPool.Count)
        {
            b = barPool[barUsed];
        }
        else
        {
            b = new RoundedPanelVisual(root.transform, "Bar_" + barPool.Count, barStyle, drawOnTop);
            SetQueue(b, QueueBase + 1);
            barPool.Add(b);
        }
        barUsed++;
        b.SetVisible(true);
        b.SetAlpha(alpha);
        return b;
    }

    private Piece TextPiece(string s, float size)
    {
        Piece p = new Piece();
        p.text = TakeText(s, size);
        p.size = p.text.rectTransform.sizeDelta;
        p.width = p.size.x;
        p.above = p.size.y * 0.5f;
        p.below = p.size.y * 0.5f;
        return p;
    }

    private Piece FractionPiece(string numerator, string denominator, float size,
        float gap, float barThickness, float sidePad)
    {
        Piece p = new Piece();
        p.fraction = true;
        p.num = TakeText(numerator, size);
        p.den = TakeText(denominator, size);
        Vector2 n = p.num.rectTransform.sizeDelta;
        Vector2 d = p.den.rectTransform.sizeDelta;
        p.width = Mathf.Max(n.x, d.x) + sidePad * 2f;
        p.above = barThickness * 0.5f + gap + n.y;
        p.below = barThickness * 0.5f + gap + d.y;
        return p;
    }

    private Block Row(params Piece[] pieces)
    {
        Block b = new Block { kind = BlockKind.Row, pieces = new List<Piece>(pieces) };
        return b;
    }

    private Block Legend(float size, params string[] lines)
    {
        Block b = new Block
        {
            kind = BlockKind.Legend,
            symbols = new List<TextMeshPro>(),
            descriptions = new List<TextMeshPro>()
        };
        foreach (string raw in lines)
        {
            string line = Expand(raw);
            int split = line.IndexOf(" = ");
            string sym = split >= 0 ? line.Substring(0, split) : "";
            string desc = split >= 0 ? "=  " + line.Substring(split + 3) : line;
            b.symbols.Add(TakeText(sym, size));
            b.descriptions.Add(TakeText(desc, size));
        }
        return b;
    }

    private Block Spacer(float h)
    {
        return new Block { kind = BlockKind.Spacer, height = h };
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void RebuildLayout()
    {
        if (root == null) return;
        layoutDirty = false;

        // Release everything; only what this state needs is taken back.
        textUsed = 0;
        barUsed = 0;
        foreach (TextMeshPro t in textPool) t.gameObject.SetActive(false);
        foreach (RoundedPanelVisual b in barPool) b.SetVisible(false);

        // ── Toggle button (always present, fixed at the origin) ──
        float btnFont = fontSize * buttonScale;
        buttonLabel.fontSize = btnFont;
        buttonLabel.text = state == PanelState.Collapsed ? texts.buttonShow
                         : state == PanelState.Basic ? texts.buttonMore
                         : texts.buttonHide;
        Vector2 labelSize = buttonLabel.GetPreferredValues(buttonLabel.text);
        float padX = Mathf.Max(0.005f, sharedStyle.paddingX * 0.6f);
        float padY = Mathf.Max(0.004f, sharedStyle.paddingY * 0.6f);
        buttonSize = new Vector2(labelSize.x + padX * 2f, labelSize.y + padY * 2f);
        buttonLabel.rectTransform.sizeDelta = buttonSize;

        Vector3 btnCentre = new Vector3(buttonSize.x * 0.5f, -buttonSize.y * 0.5f, 0f);
        buttonLabel.transform.localPosition = btnCentre;
        buttonLabel.transform.localRotation = Quaternion.identity;
        buttonCollider.center = Vector3.zero;
        buttonCollider.size = new Vector3(buttonSize.x, buttonSize.y, 0.01f);

        buttonStyle.cornerRadius = RadiusFor(buttonSize.y);
        buttonBackground.SetVisible(true);
        buttonBackground.SetSize(buttonSize.x, buttonSize.y);
        buttonBackground.root.transform.localPosition = btnCentre + Vector3.forward * ZBody;
        buttonBackground.root.transform.localRotation = Quaternion.identity;

        if (state == PanelState.Collapsed)
        {
            body.SetVisible(false);
            bodyHitArea.gameObject.SetActive(false);
            markerLoad.gameObject.SetActive(false);
            markerFree.gameObject.SetActive(false);
            ApplyAlpha();
            return;
        }

        markerLoad.gameObject.SetActive(showMarkers);
        markerFree.gameObject.SetActive(showMarkers);
        markerLoad.text = tagLoad;
        markerFree.text = tagFree;

        // ── Build the content blocks ──
        float small = fontSize * explanationScale;
        float lineH = TakeTextHeight(fontSize);
        float gap = lineH * 0.06f;
        float bar = Mathf.Max(0.0012f, lineH * 0.05f);
        float side = lineH * 0.12f;
        float sectionGap = lineH * 0.45f;
        float rowGap = lineH * 0.35f;

        string d = "Δ";
        string sf = Sub(texts.subscriptFree);
        string sl = Sub(texts.subscriptLoad);

        List<Block> blocks = new List<Block>();

        // Basic: what each letter means, before any formula.
        if (movablePulley)
            blocks.Add(Legend(small, texts.legendV, texts.legendA, texts.legendDs,
                                     texts.legendDv, texts.legendDt));
        else
            blocks.Add(Legend(small, texts.legendV, texts.legendDs, texts.legendDt));

        blocks.Add(Spacer(sectionGap));
        blocks.Add(Row(TextPiece("v  =", fontSize),
                       FractionPiece(d + "s", d + "t", fontSize, gap, bar, side)));
        if (movablePulley)
        {
            blocks.Add(Spacer(rowGap));
            blocks.Add(Row(TextPiece("a  =", fontSize),
                           FractionPiece(d + "v", d + "t", fontSize, gap, bar, side)));
        }

        Block paragraph = null;
        if (state == PanelState.Expanded)
        {
            blocks.Add(Spacer(sectionGap));
            blocks.Add(new Block { kind = BlockKind.Divider, height = bar });
            blocks.Add(Spacer(sectionGap));

            List<Piece> k = new List<Piece>();
            k.Add(TextPiece("k  =", fontSize));
            k.Add(FractionPiece(d + "s" + sf, d + "s" + sl, fontSize, gap, bar, side));
            k.Add(TextPiece("=", fontSize));
            k.Add(FractionPiece("v" + sf, "v" + sl, fontSize, gap, bar, side));
            if (movablePulley)
            {
                k.Add(TextPiece("=", fontSize));
                k.Add(FractionPiece("a" + sf, "a" + sl, fontSize, gap, bar, side));
            }
            blocks.Add(Row(k.ToArray()));

            blocks.Add(Spacer(sectionGap));
            blocks.Add(Legend(small, texts.explainK, texts.explainFree, texts.explainLoad));
            blocks.Add(Spacer(rowGap));

            paragraph = new Block { kind = BlockKind.Paragraph };
            paragraph.paragraph = TakeText(Expand(movablePulley ? texts.relationMovable
                                                                : texts.relationFixed), small);
            blocks.Add(paragraph);
        }

        // ── Measure ──
        float pieceGap = lineH * 0.15f;
        float columnGap = lineH * 0.25f;
        float legendLineGap = lineH * 0.08f;
        float contentW = 0f;

        foreach (Block b in blocks)
        {
            switch (b.kind)
            {
                case BlockKind.Row:
                {
                    float w = 0f, above = 0f, below = 0f;
                    for (int i = 0; i < b.pieces.Count; i++)
                    {
                        Piece p = b.pieces[i];
                        w += p.width + (i > 0 ? pieceGap : 0f);
                        above = Mathf.Max(above, p.above);
                        below = Mathf.Max(below, p.below);
                    }
                    b.width = w;
                    b.height = above + below;
                    break;
                }
                case BlockKind.Legend:
                {
                    float symW = 0f, descW = 0f, h = 0f;
                    for (int i = 0; i < b.symbols.Count; i++)
                    {
                        Vector2 s = b.symbols[i].rectTransform.sizeDelta;
                        Vector2 t = b.descriptions[i].rectTransform.sizeDelta;
                        symW = Mathf.Max(symW, s.x);
                        descW = Mathf.Max(descW, t.x);
                        h += Mathf.Max(s.y, t.y) + (i > 0 ? legendLineGap : 0f);
                    }
                    b.width = symW + columnGap + descW;
                    b.height = h;
                    break;
                }
            }
            if (b.kind == BlockKind.Row || b.kind == BlockKind.Legend)
                contentW = Mathf.Max(contentW, b.width);
        }

        // The relation sentence wraps to the width of everything else
        // (with a sensible minimum) instead of stretching the panel.
        if (paragraph != null)
        {
            float wrapW = Mathf.Max(contentW, lineH * 9f);
            TextMeshPro t = paragraph.paragraph;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.alignment = TextAlignmentOptions.TopLeft;
            Vector2 pref = t.GetPreferredValues(t.text, wrapW, Mathf.Infinity);
            pref.x = Mathf.Min(pref.x, wrapW);
            t.rectTransform.sizeDelta = new Vector2(wrapW, pref.y);
            paragraph.width = wrapW;
            paragraph.height = pref.y;
            contentW = Mathf.Max(contentW, wrapW);
        }

        float contentH = 0f;
        foreach (Block b in blocks) contentH += b.height;

        // ── Body backdrop, directly under the button ──
        float bodyPadX = Mathf.Max(0.008f, sharedStyle.paddingX);
        float bodyPadY = Mathf.Max(0.006f, sharedStyle.paddingY);
        bodySize = new Vector2(contentW + bodyPadX * 2f, contentH + bodyPadY * 2f);
        // Upward: the body sits above the button, so the button keeps its
        // height and the panel bottom never drops below the table.
        float bodyTop = opensUpward
            ? ButtonGap + bodySize.y
            : -buttonSize.y - ButtonGap;

        bodyStyle.cornerRadius = RadiusFor(bodySize.y);
        body.SetVisible(true);
        body.SetSize(bodySize.x, bodySize.y);
        body.root.transform.localPosition = new Vector3(bodySize.x * 0.5f,
            bodyTop - bodySize.y * 0.5f, ZBody);
        body.root.transform.localRotation = Quaternion.identity;

        bodyHitArea.gameObject.SetActive(true);
        bodyHitArea.transform.localPosition = new Vector3(bodySize.x * 0.5f,
            bodyTop - bodySize.y * 0.5f, ZBody);
        bodyHitArea.transform.localRotation = Quaternion.identity;
        bodyHitArea.center = Vector3.zero;
        bodyHitArea.size = new Vector3(bodySize.x, bodySize.y, 0.01f);

        // ── Place ──
        float left = bodyPadX;
        float y = bodyTop - bodyPadY;   // top edge of the next block

        foreach (Block b in blocks)
        {
            switch (b.kind)
            {
                case BlockKind.Row:
                {
                    float above = 0f;
                    foreach (Piece p in b.pieces) above = Mathf.Max(above, p.above);
                    float axis = y - above;
                    float x = left;
                    for (int i = 0; i < b.pieces.Count; i++)
                    {
                        Piece p = b.pieces[i];
                        if (i > 0) x += pieceGap;
                        float cx = x + p.width * 0.5f;
                        if (!p.fraction)
                        {
                            Place(p.text, cx, axis);
                        }
                        else
                        {
                            Vector2 n = p.num.rectTransform.sizeDelta;
                            Vector2 dd = p.den.rectTransform.sizeDelta;
                            Place(p.num, cx, axis + bar * 0.5f + gap + n.y * 0.5f);
                            Place(p.den, cx, axis - bar * 0.5f - gap - dd.y * 0.5f);
                            RoundedPanelVisual r = TakeBar();
                            r.SetSize(p.width, bar);
                            r.root.transform.localPosition = new Vector3(cx, axis, ZBar);
                            r.root.transform.localRotation = Quaternion.identity;
                        }
                        x += p.width;
                    }
                    break;
                }
                case BlockKind.Legend:
                {
                    float symW = 0f;
                    foreach (TextMeshPro s in b.symbols)
                        symW = Mathf.Max(symW, s.rectTransform.sizeDelta.x);
                    float ly = y;
                    for (int i = 0; i < b.symbols.Count; i++)
                    {
                        Vector2 s = b.symbols[i].rectTransform.sizeDelta;
                        Vector2 t = b.descriptions[i].rectTransform.sizeDelta;
                        float h = Mathf.Max(s.y, t.y);
                        if (i > 0) ly -= legendLineGap;
                        // Symbols right-aligned in their column so the "=" line up.
                        Place(b.symbols[i], left + symW - s.x * 0.5f, ly - h * 0.5f);
                        Place(b.descriptions[i], left + symW + columnGap + t.x * 0.5f, ly - h * 0.5f);
                        ly -= h;
                    }
                    break;
                }
                case BlockKind.Paragraph:
                {
                    TextMeshPro t = b.paragraph;
                    Place(t, left + b.width * 0.5f, y - b.height * 0.5f);
                    break;
                }
                case BlockKind.Divider:
                {
                    RoundedPanelVisual r = TakeBar();
                    r.SetSize(contentW, bar);
                    r.root.transform.localPosition = new Vector3(left + contentW * 0.5f,
                        y - bar * 0.5f, ZBar);
                    r.root.transform.localRotation = Quaternion.identity;
                    break;
                }
            }
            y -= b.height;
        }

        ApplyAlpha();
    }

    /// <summary>Height of one line at the given size, measured once per layout.</summary>
    private float TakeTextHeight(float size)
    {
        // Measure with the button label's settings rather than a pooled text,
        // so the measurement does not consume a pool slot.
        float old = buttonLabel.fontSize;
        buttonLabel.fontSize = size;
        float h = buttonLabel.GetPreferredValues("Δs").y;
        buttonLabel.fontSize = old;
        return Mathf.Max(0.001f, h);
    }

    private static void Place(TextMeshPro t, float cx, float cy)
    {
        t.transform.localPosition = new Vector3(cx, cy, 0f);
        t.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// The shader measures the radius as a fraction of the quad's full height,
    /// so a fixed physical radius has to be divided by the current height.
    /// </summary>
    private float RadiusFor(float heightMeters)
    {
        if (heightMeters <= 0.0001f) return 0f;
        return Mathf.Clamp01(cornerRadius / heightMeters);
    }

    // ── Placement ─────────────────────────────────────────────────────────

    /// <summary>
    /// Place and orient the panel. worldPosition is the top-left corner of the
    /// toggle button; the body opens above (default) or below it, so the
    /// button never moves when the student presses it.
    /// </summary>
    public void SetPosition(Vector3 worldPosition, Vector3 faceDirection,
        Vector3 rotationOffsetEuler)
    {
        if (root == null) return;
        if (layoutDirty) RebuildLayout();

        Vector3 face = faceDirection.sqrMagnitude < 0.0001f ? planeNormal : faceDirection.normalized;
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up)
                         * Quaternion.Euler(rotationOffsetEuler);

        // Keep all sizes in true metres even if a parent is scaled.
        Transform parent = root.transform.parent;
        if (parent != null)
        {
            Vector3 ls = parent.lossyScale;
            root.transform.localScale = new Vector3(
                Mathf.Abs(ls.x) > 1e-5f ? 1f / ls.x : 1f,
                Mathf.Abs(ls.y) > 1e-5f ? 1f / ls.y : 1f,
                Mathf.Abs(ls.z) > 1e-5f ? 1f / ls.z : 1f);
        }

        root.transform.position = worldPosition;
        root.transform.rotation = rot;

        // TouchButton.Start() resizes the collider once from the label's rect.
        // Re-assert the true button size so the hit area always matches the pill.
        if (buttonCollider != null)
        {
            buttonCollider.center = Vector3.zero;
            buttonCollider.size = new Vector3(buttonSize.x, buttonSize.y, 0.01f);
        }
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
    /// Re-apply sizes so Inspector tuning during Play takes effect. Only
    /// rebuilds the layout when something actually changed.
    /// </summary>
    public void SetSizes(float formulaFontSize, float explanationFontScale,
        float buttonFontScale, float markerFontSize, float cornerRadiusMeters)
    {
        bool changed =
            !Mathf.Approximately(fontSize, formulaFontSize) ||
            !Mathf.Approximately(explanationScale, explanationFontScale) ||
            !Mathf.Approximately(buttonScale, buttonFontScale) ||
            !Mathf.Approximately(cornerRadius, cornerRadiusMeters);

        fontSize = Mathf.Max(0.001f, formulaFontSize);
        explanationScale = Mathf.Max(0.1f, explanationFontScale);
        buttonScale = Mathf.Max(0.1f, buttonFontScale);
        cornerRadius = Mathf.Max(0f, cornerRadiusMeters);

        if (markerLoad != null) markerLoad.fontSize = markerFontSize;
        if (markerFree != null) markerFree.fontSize = markerFontSize;

        if (changed) layoutDirty = true;
    }

    /// <summary>Open the body above (true) or below (false) the button.</summary>
    public void SetOpensUpward(bool upward)
    {
        if (opensUpward == upward) return;
        opensUpward = upward;
        layoutDirty = true;
    }

    public void SetAlpha(float a)
    {
        alpha = Mathf.Clamp01(a);
        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        Color c = sharedStyle.textColor;
        c.a *= alpha;

        if (buttonLabel != null) { buttonLabel.color = c; buttonLabel.faceColor = c; }
        for (int i = 0; i < textUsed && i < textPool.Count; i++)
        {
            textPool[i].color = c;
            textPool[i].faceColor = c;
        }

        if (markerLoad != null) { Color m = markerLoad.color; m.a = alpha; markerLoad.color = m; }
        if (markerFree != null) { Color m = markerFree.color; m.a = alpha; markerFree.color = m; }

        if (body != null) body.SetAlpha(alpha);
        if (buttonBackground != null) buttonBackground.SetAlpha(alpha);
        for (int i = 0; i < barUsed && i < barPool.Count; i++) barPool[i].SetAlpha(alpha);
    }

    /// <summary>Diagnostic: what the panel currently thinks it is rendering.</summary>
    public string DescribeState()
    {
        if (root == null) return "root=null";
        return "active=" + root.activeSelf
             + " state=" + state
             + " movable=" + movablePulley
             + " texts=" + textUsed
             + " bars=" + barUsed
             + " body=" + bodySize.ToString("F3")
             + " button=" + buttonSize.ToString("F3")
             + " layer=" + (buttonLabel != null ? LayerMask.LayerToName(buttonLabel.gameObject.layer) : "n/a");
    }
}
