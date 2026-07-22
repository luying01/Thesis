using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ConceptualAidManager : MonoBehaviour
{
    [Header("References")]
    public PulleyPhysics pulleyPhysics;
    public FidelityManager fidelityManager;
    public Camera mainCamera;

    [Header("Aid Roots")]
    public Transform hookL;
    public Transform hookR;
    public Transform movablePulley;
    public Transform fixedPulley;

    [Header("Arrow Settings")]
    public float arrowSpacing = 0.05f;
    public float arrowWidth = 0.002f;
    public float arrowHeadSize = 0.008f;
    public float maxArrowLength = 0.15f;
    public float distanceLineOffset = 0.05f;

    [Header("Ruler Settings")]
    public float rulerTickInterval = 0.05f;
    public float rulerTickLength = 0.02f;
    public float rulerStartTickLength = 0.03f;
    public float rulerTickWidth = 0.001f;
    public float rulerLabelSize = 0.03f;
    public int maxRulerTicks = 20;

    [Header("Scale Settings")]
    public float forceScale = 0.005f;
    public float velocityScale = 0.05f;
    public float accelerationScale = 0.03f;

    [Header("Colors")]
    public Color colorDistance = Color.blue;
    public Color colorForce = Color.red;
    public Color colorVelocity = Color.green;
    public Color colorAcceleration = Color.yellow;

    [Header("Minimum Value Threshold")]
    public float minValueThreshold = 0.001f;

    [Header("Force Step Reveal")]
    public float forceStepDuration = 1f;

    private const float unitWeightMass = 0.025f;
    private const float g = 9.81f;
    private float UnitForce => unitWeightMass * g;

    private AidGroup groupHookL;
    private AidGroup groupHookR;
    private AidGroup groupMovablePulley;
    private bool aidEnabled = false;

    private string currentAidType = null;

    private GameObject forceStepRoot;
    private List<ArrowElement> forceStepArrows = new List<ArrowElement>();

    private TextMeshPro formulaLabel;
    private TextMeshPro gravityLegendLabel;

    // ── Ruler Tick Pool ───────────────────────────────────────────

    private class RulerTick
    {
        public LineRenderer tickLine;
        public TextMeshPro label;
        public GameObject root;

        public void SetActive(bool active)
        {
            if (root != null) root.SetActive(active);
        }

        public void Update(Vector3 pos, Vector3 tickDir, float tickLength,
            float distanceCm, Vector3 labelDir, bool showLabel, Camera cam)
        {
            root.SetActive(true);

            tickLine.SetPosition(0, pos - tickDir * tickLength * 0.5f);
            tickLine.SetPosition(1, pos + tickDir * tickLength * 0.5f);

            if (showLabel && label != null)
            {
                label.gameObject.SetActive(true);
                label.text = distanceCm.ToString("F0") + "cm";
                label.transform.position = pos + tickDir * (tickLength * 0.5f + 0.005f);
                if (cam != null)
                    label.transform.rotation = Quaternion.LookRotation(
                        label.transform.position - cam.transform.position);
            }
            else if (label != null)
            {
                label.gameObject.SetActive(false);
            }
        }
    }

    // ── AidGroup ──────────────────────────────────────────────────

    private class AidGroup
    {
        public Transform anchor;
        public LineRenderer distanceLine;
        public LineRenderer startTick;
        public TextMeshPro startLabel;
        public List<RulerTick> tickPool;
        public ArrowElement force;
        public ArrowElement velocity;
        public ArrowElement acceleration;
        public GameObject root;

        public void SetActive(bool active)
        {
            if (root != null) root.SetActive(active);
        }

        public void UpdateBillboard(Camera cam)
        {
            if (root == null || cam == null) return;
            root.transform.position = anchor.position;
            force?.FaceCamera(cam);
            velocity?.FaceCamera(cam);
            acceleration?.FaceCamera(cam);
        }

        public void HideAllTicks()
        {
            if (startTick != null) startTick.enabled = false;
            if (startLabel != null) startLabel.gameObject.SetActive(false);
            foreach (var tick in tickPool)
                tick.SetActive(false);
        }
    }

    // ── ArrowElement ──────────────────────────────────────────────

    private class ArrowElement
    {
        public LineRenderer line;
        public LineRenderer headLeft;
        public LineRenderer headRight;
        public TextMeshPro label;
        public Vector3 localOffset;
        public string unit;

        public void SetVisible(bool visible)
        {
            if (line != null) line.enabled = visible;
            if (headLeft != null) headLeft.enabled = visible;
            if (headRight != null) headRight.enabled = visible;
            if (label != null) label.gameObject.SetActive(visible);
        }

        public void UpdateArrow(Vector3 anchorPos, Vector3 direction, float length,
            float value, float arrowHeadSize, float minThreshold)
        {
            if (value < minThreshold)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            Vector3 start = anchorPos + localOffset;
            Vector3 end = start + direction * length;

            line.SetPosition(0, start);
            line.SetPosition(1, end);

            Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
            headLeft.enabled = true;
            headRight.enabled = true;
            headLeft.SetPosition(0, end);
            headLeft.SetPosition(1, end - direction * arrowHeadSize + right * arrowHeadSize * 0.5f);
            headRight.SetPosition(0, end);
            headRight.SetPosition(1, end - direction * arrowHeadSize - right * arrowHeadSize * 0.5f);

            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.transform.position = end + Vector3.right * 0.015f;
                label.text = value.ToString("F2") + " " + unit;
            }
        }

        public void UpdateArrowSymbolic(Vector3 anchorPos, Vector3 direction, float length,
            string symbolText, float arrowHeadSize)
        {
            if (length < 0.001f)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            Vector3 start = anchorPos + localOffset;
            Vector3 end = start + direction * length;

            line.SetPosition(0, start);
            line.SetPosition(1, end);

            Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
            headLeft.enabled = true;
            headRight.enabled = true;
            headLeft.SetPosition(0, end);
            headLeft.SetPosition(1, end - direction * arrowHeadSize + right * arrowHeadSize * 0.5f);
            headRight.SetPosition(0, end);
            headRight.SetPosition(1, end - direction * arrowHeadSize - right * arrowHeadSize * 0.5f);

            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.transform.position = end + Vector3.right * 0.015f;
                label.text = symbolText;
            }
        }

        public void FaceCamera(Camera cam)
        {
            if (label != null && cam != null)
                label.transform.rotation = Quaternion.LookRotation(
                    label.transform.position - cam.transform.position);
        }
    }

    // ── Unity Lifecycle ───────────────────────────────────────────

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        groupHookL = CreateAidGroup(hookL, "AidGroup_HookL");
        groupHookR = CreateAidGroup(hookR, "AidGroup_HookR");
        groupMovablePulley = CreateAidGroup(movablePulley, "AidGroup_MovablePulley");

        groupHookL.SetActive(false);
        groupHookR.SetActive(false);
        groupMovablePulley.SetActive(false);

        GameObject formulaObj = new GameObject("FormulaLabel");
        formulaObj.transform.SetParent(transform);
        formulaLabel = formulaObj.AddComponent<TextMeshPro>();
        formulaLabel.fontSize = 0.15f;
        formulaLabel.color = Color.white;
        formulaLabel.alignment = TextAlignmentOptions.Center;
        formulaLabel.gameObject.SetActive(false);

        GameObject legendObj = new GameObject("GravityLegendLabel");
        legendObj.transform.SetParent(transform);
        gravityLegendLabel = legendObj.AddComponent<TextMeshPro>();
        gravityLegendLabel.fontSize = 0.04f;
        gravityLegendLabel.color = Color.white;
        gravityLegendLabel.alignment = TextAlignmentOptions.Left;
        gravityLegendLabel.text = "G = m x g";
        gravityLegendLabel.gameObject.SetActive(false);
    }

    void Update()
    {
        SetFormula();

        if (!aidEnabled || pulleyPhysics == null) return;

        bool isMA2 = pulleyPhysics.IsMovablePulleyConfig();

        if (currentAidType == "velocity")
        {
            UpdateGroupHookL(isMA2, false, true, false);
            UpdateGroupHookR(isMA2, false, true, false);
            UpdateGroupMovablePulley(isMA2, false, true, false);
        }
        else if (currentAidType == "distance")
        {
            UpdateGroupHookL(isMA2, false, false, true);
            UpdateGroupHookR(isMA2, false, false, true);
            UpdateGroupMovablePulley(isMA2, false, false, true);
        }
        else
        {
            groupHookL.force.SetVisible(false);
            groupHookL.velocity.SetVisible(false);
            groupHookL.acceleration.SetVisible(false);
            groupHookL.distanceLine.enabled = false;
            groupHookL.HideAllTicks();

            groupHookR.force.SetVisible(false);
            groupHookR.velocity.SetVisible(false);
            groupHookR.acceleration.SetVisible(false);
            groupHookR.distanceLine.enabled = false;
            groupHookR.HideAllTicks();

            groupMovablePulley.force.SetVisible(false);
            groupMovablePulley.velocity.SetVisible(false);
            groupMovablePulley.acceleration.SetVisible(false);
            groupMovablePulley.distanceLine.enabled = false;
            groupMovablePulley.HideAllTicks();
        }

        groupHookL.UpdateBillboard(mainCamera);
        groupHookR.UpdateBillboard(mainCamera);
        groupMovablePulley.UpdateBillboard(mainCamera);
    }

    // ── Create Aid Group ──────────────────────────────────────────

    private AidGroup CreateAidGroup(Transform anchor, string groupName)
    {
        AidGroup group = new AidGroup();
        group.anchor = anchor;
        group.tickPool = new List<RulerTick>();

        group.root = new GameObject(groupName);
        group.root.transform.SetParent(anchor);
        group.root.transform.localPosition = Vector3.zero;

        GameObject distLineObj = new GameObject("DistanceLine");
        distLineObj.transform.SetParent(group.root.transform);
        group.distanceLine = distLineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(group.distanceLine, colorDistance, arrowWidth);
        group.distanceLine.positionCount = 2;
        group.distanceLine.enabled = false;

        GameObject startTickObj = new GameObject("StartTick");
        startTickObj.transform.SetParent(group.root.transform);
        group.startTick = startTickObj.AddComponent<LineRenderer>();
        SetupLineRenderer(group.startTick, colorDistance, rulerTickWidth);
        group.startTick.positionCount = 2;
        group.startTick.enabled = false;

        GameObject startLabelObj = new GameObject("StartLabel");
        startLabelObj.transform.SetParent(group.root.transform);
        group.startLabel = startLabelObj.AddComponent<TextMeshPro>();
        group.startLabel.fontSize = rulerLabelSize;
        group.startLabel.color = colorDistance;
        group.startLabel.alignment = TextAlignmentOptions.Left;
        group.startLabel.gameObject.SetActive(false);

        for (int i = 0; i < maxRulerTicks; i++)
        {
            RulerTick tick = CreateRulerTick(group.root, i);
            tick.SetActive(false);
            group.tickPool.Add(tick);
        }

        group.force = CreateArrowElement(group.root, "Force", colorForce, "N",
            new Vector3(-0.5f * arrowSpacing, 0, 0));
        group.velocity = CreateArrowElement(group.root, "Velocity", colorVelocity, "m/s",
            new Vector3(0.5f * arrowSpacing, 0, 0));
        group.acceleration = CreateArrowElement(group.root, "Acceleration", colorAcceleration, "m/s²",
            new Vector3(1.5f * arrowSpacing, 0, 0));

        return group;
    }

    private RulerTick CreateRulerTick(GameObject parent, int index)
    {
        RulerTick tick = new RulerTick();

        tick.root = new GameObject("Tick_" + index);
        tick.root.transform.SetParent(parent.transform);

        GameObject lineObj = new GameObject("TickLine");
        lineObj.transform.SetParent(tick.root.transform);
        tick.tickLine = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(tick.tickLine, colorDistance, rulerTickWidth);
        tick.tickLine.positionCount = 2;

        GameObject labelObj = new GameObject("TickLabel");
        labelObj.transform.SetParent(tick.root.transform);
        tick.label = labelObj.AddComponent<TextMeshPro>();
        tick.label.fontSize = rulerLabelSize;
        tick.label.color = colorDistance;
        tick.label.alignment = TextAlignmentOptions.Left;

        return tick;
    }

    private ArrowElement CreateArrowElement(GameObject parent, string name,
        Color color, string unit, Vector3 offset)
    {
        ArrowElement element = new ArrowElement();
        element.localOffset = offset;
        element.unit = unit;

        GameObject lineObj = new GameObject(name + "_Line");
        lineObj.transform.SetParent(parent.transform);
        element.line = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(element.line, color, arrowWidth);
        element.line.positionCount = 2;

        GameObject headLObj = new GameObject(name + "_HeadL");
        headLObj.transform.SetParent(parent.transform);
        element.headLeft = headLObj.AddComponent<LineRenderer>();
        SetupLineRenderer(element.headLeft, color, arrowWidth);
        element.headLeft.positionCount = 2;

        GameObject headRObj = new GameObject(name + "_HeadR");
        headRObj.transform.SetParent(parent.transform);
        element.headRight = headRObj.AddComponent<LineRenderer>();
        SetupLineRenderer(element.headRight, color, arrowWidth);
        element.headRight.positionCount = 2;

        GameObject labelObj = new GameObject(name + "_Label");
        labelObj.transform.SetParent(parent.transform);
        element.label = labelObj.AddComponent<TextMeshPro>();
        element.label.fontSize = 0.05f;
        element.label.color = color;
        element.label.alignment = TextAlignmentOptions.Left;
        element.label.gameObject.SetActive(false);

        return element;
    }

    private void SetupLineRenderer(LineRenderer lr, Color color, float width)
    {
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.useWorldSpace = true;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.positionCount = 2;
    }

    // ── Update Groups (continuous velocity/force/accel display) ────

    private void UpdateGroupHookL(bool isMA2, bool showForce, bool showVelocity, bool showDistance)
    {
        if (groupHookL.anchor == null) return;
        Vector3 anchorPos = groupHookL.anchor.position;
        float v = pulleyPhysics.velocity;
        float a = pulleyPhysics.acceleration;

        if (showDistance)
        {
            UpdateDistanceRuler(groupHookL, anchorPos, Vector3.left,
                pulleyPhysics.IsTrackingHookL(),
                pulleyPhysics.GetStartPositionHookL(),
                pulleyPhysics.distanceHookL);
        }
        else
        {
            groupHookL.distanceLine.enabled = false;
            groupHookL.HideAllTicks();
        }

        if (showForce)
        {
            float force = isMA2
                ? pulleyPhysics.tensionForce
                : pulleyPhysics.massLeft * g;

            groupHookL.force.UpdateArrow(anchorPos, Vector3.down,
                Mathf.Clamp(force * forceScale, 0, maxArrowLength),
                force, arrowHeadSize, minValueThreshold);
        }
        else
        {
            groupHookL.force.SetVisible(false);
        }

        if (showVelocity)
        {
            Vector3 moveDir = v > 0 ? Vector3.down : Vector3.up;
            Vector3 accDir = a > 0 ? Vector3.down : Vector3.up;

            groupHookL.velocity.UpdateArrow(anchorPos, moveDir,
                Mathf.Clamp(Mathf.Abs(v) * velocityScale, 0, maxArrowLength),
                Mathf.Abs(v), arrowHeadSize, minValueThreshold);

            groupHookL.acceleration.UpdateArrow(anchorPos, accDir,
                Mathf.Clamp(Mathf.Abs(a) * accelerationScale, 0, maxArrowLength),
                Mathf.Abs(a), arrowHeadSize, minValueThreshold);
        }
        else
        {
            groupHookL.velocity.SetVisible(false);
            groupHookL.acceleration.SetVisible(false);
        }
    }

    private void UpdateGroupHookR(bool isMA2, bool showForce, bool showVelocity, bool showDistance)
    {
        if (groupHookR.anchor == null) return;
        Vector3 anchorPos = groupHookR.anchor.position;
        float v = pulleyPhysics.velocity;
        float a = pulleyPhysics.acceleration;

        if (showDistance)
        {
            UpdateDistanceRuler(groupHookR, anchorPos, Vector3.right,
                pulleyPhysics.IsTrackingHookR(),
                pulleyPhysics.GetStartPositionHookR(),
                pulleyPhysics.distanceHookR);
        }
        else
        {
            groupHookR.distanceLine.enabled = false;
            groupHookR.HideAllTicks();
        }

        if (showForce)
        {
            float force = isMA2
                ? pulleyPhysics.tensionForce
                : pulleyPhysics.massRight * g;

            groupHookR.force.UpdateArrow(anchorPos, Vector3.down,
                Mathf.Clamp(force * forceScale, 0, maxArrowLength),
                force, arrowHeadSize, minValueThreshold);
        }
        else
        {
            groupHookR.force.SetVisible(false);
        }

        if (showVelocity)
        {
            Vector3 moveDir = v > 0 ? Vector3.up : Vector3.down;
            Vector3 accDir = a > 0 ? Vector3.up : Vector3.down;

            groupHookR.velocity.UpdateArrow(anchorPos, moveDir,
                Mathf.Clamp(Mathf.Abs(v) * velocityScale, 0, maxArrowLength),
                Mathf.Abs(v), arrowHeadSize, minValueThreshold);

            groupHookR.acceleration.UpdateArrow(anchorPos, accDir,
                Mathf.Clamp(Mathf.Abs(a) * accelerationScale, 0, maxArrowLength),
                Mathf.Abs(a), arrowHeadSize, minValueThreshold);
        }
        else
        {
            groupHookR.velocity.SetVisible(false);
            groupHookR.acceleration.SetVisible(false);
        }
    }

    private void UpdateGroupMovablePulley(bool isMA2, bool showForce, bool showVelocity, bool showDistance)
    {
        if (groupMovablePulley.anchor == null) return;
        if (!isMA2)
        {
            groupMovablePulley.distanceLine.enabled = false;
            groupMovablePulley.HideAllTicks();
            groupMovablePulley.force.SetVisible(false);
            groupMovablePulley.velocity.SetVisible(false);
            groupMovablePulley.acceleration.SetVisible(false);
            return;
        }

        Vector3 anchorPos = groupMovablePulley.anchor.position;
        float v = pulleyPhysics.velocity;
        float a = pulleyPhysics.acceleration;

        Vector3 offsetDir = Vector3.right;
        if (fixedPulley != null)
        {
            Vector3 diff = movablePulley.position - fixedPulley.position;
            diff.y = 0f;
            if (diff.magnitude > 0.001f)
                offsetDir = diff.normalized;
        }

        if (showDistance)
        {
            UpdateDistanceRuler(groupMovablePulley, anchorPos, offsetDir,
                pulleyPhysics.IsTrackingMovablePulley(),
                pulleyPhysics.GetStartPositionMovablePulley(),
                pulleyPhysics.distanceMovablePulley);
        }
        else
        {
            groupMovablePulley.distanceLine.enabled = false;
            groupMovablePulley.HideAllTicks();
        }

        if (showForce)
        {
            float force = pulleyPhysics.GetLoadMass() * g;
            groupMovablePulley.force.UpdateArrow(anchorPos, Vector3.down,
                Mathf.Clamp(force * forceScale, 0, maxArrowLength),
                force, arrowHeadSize, minValueThreshold);
        }
        else
        {
            groupMovablePulley.force.SetVisible(false);
        }

        if (showVelocity)
        {
            float vLoad = Mathf.Abs(v) * 0.5f;
            Vector3 moveDir = v > 0 ? Vector3.down : Vector3.up;
            groupMovablePulley.velocity.UpdateArrow(anchorPos, moveDir,
                Mathf.Clamp(vLoad * velocityScale, 0, maxArrowLength),
                vLoad, arrowHeadSize, minValueThreshold);

            float aLoad = Mathf.Abs(a) * 0.5f;
            Vector3 accDir = a > 0 ? Vector3.down : Vector3.up;
            groupMovablePulley.acceleration.UpdateArrow(anchorPos, accDir,
                Mathf.Clamp(aLoad * accelerationScale, 0, maxArrowLength),
                aLoad, arrowHeadSize, minValueThreshold);
        }
        else
        {
            groupMovablePulley.velocity.SetVisible(false);
            groupMovablePulley.acceleration.SetVisible(false);
        }
    }

    // ── Distance Ruler ────────────────────────────────────────────

    private void UpdateDistanceRuler(AidGroup group, Vector3 anchorPos,
        Vector3 offsetDir, bool isTracking, Vector3 startPos, float distance)
    {
        if (!isTracking || distance < minValueThreshold)
        {
            group.distanceLine.enabled = false;
            group.HideAllTicks();
            return;
        }

        Vector3 offset = offsetDir * distanceLineOffset;
        Vector3 lineStart = startPos + offset;
        Vector3 lineEnd = anchorPos + offset;

        group.distanceLine.enabled = true;
        group.distanceLine.SetPosition(0, lineStart);
        group.distanceLine.SetPosition(1, lineEnd);

        Vector3 tickDir = offsetDir;

        group.startTick.enabled = true;
        group.startTick.SetPosition(0, lineStart - tickDir * rulerStartTickLength * 0.5f);
        group.startTick.SetPosition(1, lineStart + tickDir * rulerStartTickLength * 0.5f);

        group.startLabel.gameObject.SetActive(true);
        group.startLabel.text = "0cm";
        group.startLabel.transform.position = lineStart + tickDir * (rulerStartTickLength * 0.5f + 0.005f);
        if (mainCamera != null)
            group.startLabel.transform.rotation = Quaternion.LookRotation(
                group.startLabel.transform.position - mainCamera.transform.position);

        Vector3 lineDir = (lineEnd - lineStart).normalized;

        int tickCount = Mathf.FloorToInt(distance / rulerTickInterval);
        tickCount = Mathf.Min(tickCount, maxRulerTicks);

        for (int i = 0; i < maxRulerTicks; i++)
        {
            if (i < tickCount)
            {
                float tickDistance = (i + 1) * rulerTickInterval;
                Vector3 tickPos = lineStart + lineDir * tickDistance;
                float distanceCm = tickDistance * 100f;

                group.tickPool[i].Update(tickPos, tickDir, rulerTickLength,
                    distanceCm, tickDir, true, mainCamera);
            }
            else
            {
                group.tickPool[i].SetActive(false);
            }
        }
    }

    // ── Formula Display (velocity/acceleration questions) ──────────

    public void SetCurrentAidType(string aidType)
    {
        Debug.Log($"[AidDebug] SetCurrentAidType called with: {aidType}");
        currentAidType = aidType;
    }

    private void SetFormula()
    {
        if (formulaLabel == null) return;

        if (currentAidType != "velocity" || !aidEnabled || pulleyPhysics == null)
        {
            formulaLabel.gameObject.SetActive(false);
            return;
        }

        bool isMA2 = pulleyPhysics.IsMovablePulleyConfig();
        string text = isMA2
            ? "v_free = 2 x v_load\na_free = 2 x a_load"
            : "v_free = v_load";

        formulaLabel.text = text;
        formulaLabel.gameObject.SetActive(true);

        Vector3 anchor = fixedPulley != null ? fixedPulley.position : transform.position;
        formulaLabel.transform.position = anchor + Vector3.up * 0.08f + Vector3.right * 0.12f;

        if (mainCamera != null)
            formulaLabel.transform.rotation = Quaternion.LookRotation(
                formulaLabel.transform.position - mainCamera.transform.position);
    }

    // ── Force Step Reveal (1-1 / 2-1 / 2-2 style force diagrams) ──

    public bool IsAidEnabled() => aidEnabled;

    public IEnumerator PlayForceReveal(string aidType)
    {
        Debug.Log($"[AidDebug] Step1: entered, aidType={aidType}");

        if (aidType != "force")
        {
            Debug.Log("[AidDebug] Step2: aidType != force, breaking");
            yield break;
        }

        if (pulleyPhysics == null)
        {
            Debug.Log("[AidDebug] Step3: pulleyPhysics IS NULL, breaking");
            yield break;
        }

        Debug.Log("[AidDebug] Step4: pulleyPhysics is NOT null, checking isMA2");
        bool isMA2 = pulleyPhysics.IsMovablePulleyConfig();
        Debug.Log($"[AidDebug] Step5: isMA2={isMA2}, about to start sub-coroutine");

        if (isMA2)
            yield return StartCoroutine(PlayMovablePulleyForceReveal());
        else
            yield return StartCoroutine(PlayAtwoodForceReveal());

        Debug.Log("[AidDebug] Step6: sub-coroutine finished, hiding arrows");

        HideForceStepArrows();
        if (gravityLegendLabel != null) gravityLegendLabel.gameObject.SetActive(false);
    }

    private void EnsureForceStepArrows(int count)
    {
        if (forceStepRoot == null)
        {
            forceStepRoot = new GameObject("ForceStepArrows");
            forceStepRoot.transform.SetParent(transform);
            forceStepRoot.transform.localPosition = Vector3.zero;
        }
        while (forceStepArrows.Count < count)
        {
            ArrowElement el = CreateArrowElement(forceStepRoot, "ForceStep_" + forceStepArrows.Count,
                colorForce, "", Vector3.zero);
            el.SetVisible(false);
            forceStepArrows.Add(el);
        }
    }

    private void HideForceStepArrows()
    {
        foreach (var a in forceStepArrows) a.SetVisible(false);
    }

    private string BuildForceLabel(string symbol, float forceValue)
    {
        int multiple = Mathf.RoundToInt(forceValue / UnitForce);
        return $"{symbol} = {multiple}G";
    }

    private void ShowGravityLegend(Vector3 nearPosition)
    {
        if (gravityLegendLabel == null) return;
        gravityLegendLabel.gameObject.SetActive(true);
        gravityLegendLabel.transform.position = nearPosition + Vector3.up * 0.04f + Vector3.left * 0.05f;
        if (mainCamera != null)
            gravityLegendLabel.transform.rotation = Quaternion.LookRotation(
                gravityLegendLabel.transform.position - mainCamera.transform.position);
    }

    private void HideGravityLegend()
    {
        if (gravityLegendLabel != null) gravityLegendLabel.gameObject.SetActive(false);
    }

    private IEnumerator PlayAtwoodForceReveal()
    {
        float massRight = pulleyPhysics.massRight;
        float massLeft = pulleyPhysics.massLeft;

        float forceR = massRight * g;
        float forceL = massLeft * g;

        EnsureForceStepArrows(4);
        HideForceStepArrows();

        Vector3 nearSide = Vector3.right * 0.02f;
        Vector3 farSide = Vector3.left * 0.02f;

        forceStepArrows[0].localOffset = nearSide;
        forceStepArrows[0].UpdateArrowSymbolic(hookR.position, Vector3.down,
            Mathf.Clamp(forceR * forceScale, 0, maxArrowLength),
            BuildForceLabel("G_R", forceR), arrowHeadSize);
        ShowGravityLegend(hookR.position);
        yield return new WaitForSeconds(forceStepDuration);
        HideGravityLegend();

        forceStepArrows[1].localOffset = farSide;
        forceStepArrows[1].UpdateArrowSymbolic(hookR.position, Vector3.up,
            Mathf.Clamp(forceR * forceScale, 0, maxArrowLength),
            BuildForceLabel("F_R", forceR), arrowHeadSize);
        yield return new WaitForSeconds(forceStepDuration);

        forceStepArrows[2].localOffset = farSide;
        forceStepArrows[2].UpdateArrowSymbolic(hookL.position, Vector3.up,
            Mathf.Clamp(forceL * forceScale, 0, maxArrowLength),
            BuildForceLabel("F_L", forceL), arrowHeadSize);
        yield return new WaitForSeconds(forceStepDuration);

        forceStepArrows[3].localOffset = nearSide;
        forceStepArrows[3].UpdateArrowSymbolic(hookL.position, Vector3.down,
            Mathf.Clamp(forceL * forceScale, 0, maxArrowLength),
            BuildForceLabel("G_L", forceL), arrowHeadSize);
        yield return new WaitForSeconds(forceStepDuration);
    }

    private IEnumerator PlayMovablePulleyForceReveal()
    {
        float loadMass = pulleyPhysics.GetLoadMass();
        float forceMass = pulleyPhysics.GetForceMass();

        float loadForce = loadMass * g;
        float halfLoadForce = loadForce * 0.5f;
        float freeForce = forceMass * g;

        EnsureForceStepArrows(5);
        HideForceStepArrows();

        forceStepArrows[0].localOffset = Vector3.right * 0.02f;
        forceStepArrows[0].UpdateArrowSymbolic(movablePulley.position, Vector3.down,
            Mathf.Clamp(loadForce * forceScale, 0, maxArrowLength),
            BuildForceLabel("G_load", loadForce), arrowHeadSize);
        ShowGravityLegend(movablePulley.position);
        yield return new WaitForSeconds(forceStepDuration);
        HideGravityLegend();

        forceStepArrows[1].localOffset = Vector3.left * 0.02f;
        forceStepArrows[1].UpdateArrowSymbolic(movablePulley.position, Vector3.up,
            Mathf.Clamp(halfLoadForce * forceScale, 0, maxArrowLength),
            BuildForceLabel("F_1", halfLoadForce), arrowHeadSize);

        forceStepArrows[2].localOffset = Vector3.right * 0.06f;
        forceStepArrows[2].UpdateArrowSymbolic(movablePulley.position, Vector3.up,
            Mathf.Clamp(halfLoadForce * forceScale, 0, maxArrowLength),
            BuildForceLabel("F_2", halfLoadForce), arrowHeadSize);
        yield return new WaitForSeconds(forceStepDuration);

        Vector3 fixedAnchor = fixedPulley != null ? fixedPulley.position : movablePulley.position + Vector3.up * 0.2f;
        forceStepArrows[3].localOffset = Vector3.right * 0.02f;
        forceStepArrows[3].UpdateArrowSymbolic(fixedAnchor, Vector3.up,
            Mathf.Clamp(halfLoadForce * forceScale, 0, maxArrowLength),
            BuildForceLabel("F_conn", halfLoadForce), arrowHeadSize);
        yield return new WaitForSeconds(forceStepDuration);

        Transform freeEnd = pulleyPhysics.GetFreeEndHook();
        Vector3 freeAnchor = freeEnd != null ? freeEnd.position : hookR.position;
        forceStepArrows[4].localOffset = Vector3.right * 0.02f;
        forceStepArrows[4].UpdateArrowSymbolic(freeAnchor, Vector3.down,
            Mathf.Clamp(freeForce * forceScale, 0, maxArrowLength),
            BuildForceLabel("G_free", freeForce), arrowHeadSize);
        yield return new WaitForSeconds(forceStepDuration);
    }

    // ── Fidelity Level Change ─────────────────────────────────────

    public void OnFidelityLevelChanged(int newLevel)
    {
        bool shouldEnable = (newLevel == 0 || newLevel == 2);
        SetAidEnabled(shouldEnable);
    }

    public void SetAidEnabled(bool enabled)
    {
        aidEnabled = enabled;
        groupHookL?.SetActive(enabled);
        groupHookR?.SetActive(enabled);
        groupMovablePulley?.SetActive(enabled);
        if (!enabled)
        {
            HideForceStepArrows();
            HideGravityLegend();
        }
        Debug.Log($"[ConceptualAidManager] Aid {(enabled ? "ON" : "OFF")}");
    }
}