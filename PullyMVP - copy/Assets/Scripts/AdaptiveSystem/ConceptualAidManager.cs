using UnityEngine;
using TMPro;
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
    public float arrowSpacing = 0.03f;
    public float arrowWidth = 0.002f;
    public float arrowHeadSize = 0.008f;
    public float maxArrowLength = 0.15f;
    public float distanceLineOffset = 0.05f;    // Increased offset from rope

    [Header("Ruler Settings")]
    public float rulerTickInterval = 0.05f;     // 5cm between ticks
    public float rulerTickLength = 0.02f;       // 2cm tick length
    public float rulerStartTickLength = 0.03f;  // 3cm start tick length
    public float rulerTickWidth = 0.001f;
    public float rulerLabelSize = 0.03f;
    public int maxRulerTicks = 20;              // Object pool size

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

    private AidGroup groupHookL;
    private AidGroup groupHookR;
    private AidGroup groupMovablePulley;
    private bool aidEnabled = false;

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

            // Tick line: centered on the distance line
            tickLine.SetPosition(0, pos - tickDir * tickLength * 0.5f);
            tickLine.SetPosition(1, pos + tickDir * tickLength * 0.5f);

            // Label
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
        public LineRenderer startTick;          // Start position tick (3cm)
        public TextMeshPro startLabel;          // "0cm" label
        public List<RulerTick> tickPool;        // Object pool for interval ticks
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
    }

    void Update()
    {
        if (!aidEnabled || pulleyPhysics == null) return;

        bool isMA2 = pulleyPhysics.IsMovablePulleyConfig();

        UpdateGroupHookL(isMA2);
        UpdateGroupHookR(isMA2);
        UpdateGroupMovablePulley(isMA2);

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

        // Distance line
        GameObject distLineObj = new GameObject("DistanceLine");
        distLineObj.transform.SetParent(group.root.transform);
        group.distanceLine = distLineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(group.distanceLine, colorDistance, arrowWidth);
        group.distanceLine.positionCount = 2;
        group.distanceLine.enabled = false;

        // Start tick (3cm)
        GameObject startTickObj = new GameObject("StartTick");
        startTickObj.transform.SetParent(group.root.transform);
        group.startTick = startTickObj.AddComponent<LineRenderer>();
        SetupLineRenderer(group.startTick, colorDistance, rulerTickWidth);
        group.startTick.positionCount = 2;
        group.startTick.enabled = false;

        // Start label
        GameObject startLabelObj = new GameObject("StartLabel");
        startLabelObj.transform.SetParent(group.root.transform);
        group.startLabel = startLabelObj.AddComponent<TextMeshPro>();
        group.startLabel.fontSize = rulerLabelSize;
        group.startLabel.color = colorDistance;
        group.startLabel.alignment = TextAlignmentOptions.Left;
        group.startLabel.gameObject.SetActive(false);

        // Object pool for interval ticks
        for (int i = 0; i < maxRulerTicks; i++)
        {
            RulerTick tick = CreateRulerTick(group.root, i);
            tick.SetActive(false);
            group.tickPool.Add(tick);
        }

        // Arrows
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

        // Tick line
        GameObject lineObj = new GameObject("TickLine");
        lineObj.transform.SetParent(tick.root.transform);
        tick.tickLine = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(tick.tickLine, colorDistance, rulerTickWidth);
        tick.tickLine.positionCount = 2;

        // Label
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

    // ── Update Groups ─────────────────────────────────────────────

    private void UpdateGroupHookL(bool isMA2)
    {
        if (groupHookL.anchor == null) return;
        Vector3 anchorPos = groupHookL.anchor.position;
        float v = pulleyPhysics.velocity;
        float a = pulleyPhysics.acceleration;

        UpdateDistanceRuler(groupHookL, anchorPos, Vector3.left,
            pulleyPhysics.IsTrackingHookL(),
            pulleyPhysics.GetStartPositionHookL(),
            pulleyPhysics.distanceHookL);

        float force = isMA2
            ? pulleyPhysics.tensionForce
            : pulleyPhysics.massLeft * 9.81f;

        groupHookL.force.UpdateArrow(anchorPos, Vector3.down,
            Mathf.Clamp(force * forceScale, 0, maxArrowLength),
            force, arrowHeadSize, minValueThreshold);

        Vector3 moveDir = v > 0 ? Vector3.down : Vector3.up;
        Vector3 accDir = a > 0 ? Vector3.down : Vector3.up;

        groupHookL.velocity.UpdateArrow(anchorPos, moveDir,
            Mathf.Clamp(Mathf.Abs(v) * velocityScale, 0, maxArrowLength),
            Mathf.Abs(v), arrowHeadSize, minValueThreshold);

        groupHookL.acceleration.UpdateArrow(anchorPos, accDir,
            Mathf.Clamp(Mathf.Abs(a) * accelerationScale, 0, maxArrowLength),
            Mathf.Abs(a), arrowHeadSize, minValueThreshold);
    }

    private void UpdateGroupHookR(bool isMA2)
    {
        if (groupHookR.anchor == null) return;
        Vector3 anchorPos = groupHookR.anchor.position;
        float v = pulleyPhysics.velocity;
        float a = pulleyPhysics.acceleration;

        UpdateDistanceRuler(groupHookR, anchorPos, Vector3.right,
            pulleyPhysics.IsTrackingHookR(),
            pulleyPhysics.GetStartPositionHookR(),
            pulleyPhysics.distanceHookR);

        float force = isMA2
            ? pulleyPhysics.tensionForce
            : pulleyPhysics.massRight * 9.81f;

        groupHookR.force.UpdateArrow(anchorPos, Vector3.down,
            Mathf.Clamp(force * forceScale, 0, maxArrowLength),
            force, arrowHeadSize, minValueThreshold);

        Vector3 moveDir = v > 0 ? Vector3.up : Vector3.down;
        Vector3 accDir = a > 0 ? Vector3.up : Vector3.down;

        groupHookR.velocity.UpdateArrow(anchorPos, moveDir,
            Mathf.Clamp(Mathf.Abs(v) * velocityScale, 0, maxArrowLength),
            Mathf.Abs(v), arrowHeadSize, minValueThreshold);

        groupHookR.acceleration.UpdateArrow(anchorPos, accDir,
            Mathf.Clamp(Mathf.Abs(a) * accelerationScale, 0, maxArrowLength),
            Mathf.Abs(a), arrowHeadSize, minValueThreshold);
    }

    private void UpdateGroupMovablePulley(bool isMA2)
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

        // Offset direction: away from fixed pulley horizontally
        Vector3 offsetDir = Vector3.right;
        if (fixedPulley != null)
        {
            Vector3 diff = movablePulley.position - fixedPulley.position;
            diff.y = 0f;
            if (diff.magnitude > 0.001f)
                offsetDir = diff.normalized;
        }

        UpdateDistanceRuler(groupMovablePulley, anchorPos, offsetDir,
            pulleyPhysics.IsTrackingMovablePulley(),
            pulleyPhysics.GetStartPositionMovablePulley(),
            pulleyPhysics.distanceMovablePulley);

        float force = pulleyPhysics.GetLoadMass() * 9.81f;
        groupMovablePulley.force.UpdateArrow(anchorPos, Vector3.down,
            Mathf.Clamp(force * forceScale, 0, maxArrowLength),
            force, arrowHeadSize, minValueThreshold);

        float vLoad = Mathf.Abs(v) * 0.5f;
        Vector3 moveDir = v > 0 ? Vector3.down : Vector3.up;
        groupMovablePulley.velocity.UpdateArrow(anchorPos, moveDir,
            Mathf.Clamp(vLoad * velocityScale, 0, maxArrowLength),
            vLoad, arrowHeadSize, minValueThreshold);

        Vector3 accDir = a > 0 ? Vector3.down : Vector3.up;
        groupMovablePulley.acceleration.UpdateArrow(anchorPos, accDir,
            Mathf.Clamp(Mathf.Abs(a) * accelerationScale, 0, maxArrowLength),
            Mathf.Abs(a), arrowHeadSize, minValueThreshold);
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

        // Main distance line
        group.distanceLine.enabled = true;
        group.distanceLine.SetPosition(0, lineStart);
        group.distanceLine.SetPosition(1, lineEnd);

        // Tick direction: perpendicular to distance line and offset direction
        // Distance line is vertical, offset is horizontal, so tick is horizontal
        Vector3 tickDir = offsetDir;

        // Start tick (3cm)
        group.startTick.enabled = true;
        group.startTick.SetPosition(0, lineStart - tickDir * rulerStartTickLength * 0.5f);
        group.startTick.SetPosition(1, lineStart + tickDir * rulerStartTickLength * 0.5f);

        // Start label "0cm"
        group.startLabel.gameObject.SetActive(true);
        group.startLabel.text = "0cm";
        group.startLabel.transform.position = lineStart + tickDir * (rulerStartTickLength * 0.5f + 0.005f);
        if (mainCamera != null)
            group.startLabel.transform.rotation = Quaternion.LookRotation(
                group.startLabel.transform.position - mainCamera.transform.position);

        // Direction from start to end (up or down)
        Vector3 lineDir = (lineEnd - lineStart).normalized;

        // Calculate how many ticks fit
        int tickCount = Mathf.FloorToInt(distance / rulerTickInterval);
        tickCount = Mathf.Min(tickCount, maxRulerTicks);

        // Update ticks from pool
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
        Debug.Log($"[ConceptualAidManager] Aid {(enabled ? "ON" : "OFF")}");
    }
}