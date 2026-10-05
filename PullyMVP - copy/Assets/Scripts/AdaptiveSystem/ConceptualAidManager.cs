using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Conceptual aid controller for PulleyMVP.
///
/// Three aid families, selected by the current question's aidType:
///   "force"    -> force arrows (1-1 static, 2-1 / 2-2 three-stage merge)
///   "distance" -> segmented displacement tracks
///   "velocity" -> segmented displacement tracks + symbolic formula panel
///
/// The fidelity level only switches the aid on and off (on at level 0 and 1).
/// Level 2 = high fidelity, no aid; 1 = high fidelity + aid; 0 = low fidelity + aid.
/// It never decides what the aid contains - that comes from aidType.
///
/// All visuals are generated procedurally, so nothing has to be dragged into
/// the Inspector beyond the scene anchors listed below.
/// </summary>
public class ConceptualAidManager : MonoBehaviour
{
    // ── Inspector: core references ────────────────────────────────────────

    [Header("Core References")]
    public PulleyPhysics pulleyPhysics;
    public FidelityManager fidelityManager;
    public ExperimentConfigManager experimentConfigManager;
    public QuizManager quizManager;
    public Camera mainCamera;

    [Header("Scene Anchors")]
    public Transform hookL;
    public Transform hookR;
    public Transform fixedPulley;
    public Transform movablePulley;
    public Transform movableRopeSlotL;
    public Transform movableRopeSlotR;

    [Header("Visibility")]
    [Tooltip("Draw the aid through the rack, pulleys and rope so it is never " +
             "hidden behind the apparatus it describes.")]
    public bool renderOnTop = true;
    [Tooltip("Direction from the apparatus toward the viewer. In this scene -X.")]
    public Vector3 towardViewer = new Vector3(-1f, 0f, 0f);
    [Tooltip("How far toward the viewer the whole aid is nudged, in metres.")]
    public float aidForwardOffset = 0.06f;
    [Tooltip("Arrows are never drawn below this world Y (the table surface). " +
             "An arrow that would fall below it is shifted up bodily - its " +
             "length is never trimmed.")]
    public float minWorldY = 0.78f;

    [Header("Arrow / Label Plane")]
    [Tooltip("Normal of the plane the arrows and labels face. Keep it fixed - " +
             "per-eye billboarding makes the two eyes see different shapes in VR.")]
    public Vector3 arrowPlaneNormal = new Vector3(0f, 0f, -1f);

    // ── Inspector: force aid ──────────────────────────────────────────────

    [Header("Force Aid - Rest Detection")]
    [Tooltip("System counts as at rest below this speed (m/s).")]
    public float restVelocityThreshold = 0.02f;
    [Tooltip("How long the system must stay at rest before the aid fades in.")]
    public float restHoldTime = 0.1f;

    [Header("Force Aid - Timing")]
    public float fadeInDuration = 0.15f;
    public float fadeOutDuration = 0.2f;
    [Tooltip("Stage 1 dwell. Do not shorten: the student must see that there " +
             "are TWO forces on the rope before they merge.")]
    public float stage1HoldDuration = 1.2f;
    [Tooltip("Stage 2 translation. Translation only - arrows never scale.")]
    public float stage2MergeDuration = 0.8f;
    [Tooltip("How long the terminal state is held during a demo sequence, " +
             "before ExperimentConfigManager retracts the equipment.")]
    public float demoTerminalHoldTime = 4.0f;
    [Tooltip("After a demo-driven reveal, leave the diagram frozen in the " +
             "comparison layout instead of letting it fade when the demo " +
             "retracts the equipment. Cleared on question change, on replay, " +
             "or as soon as the student grabs something.")]
    public bool holdDiagramAfterDemo = true;

    [Tooltip("Give up waiting for the system to settle after this long.")]
    public float revealSettleTimeout = 3f;
    [Tooltip("Rest confirmation used by the demo-driven reveal. Shorter than " +
             "restHoldTime: ExperimentConfigManager has already finished its " +
             "placement animation by the time it calls in, so the arrows should " +
             "appear as soon as the weights stop.")]
    public float revealSettleHoldTime = 0.05f;

    [Header("Force Aid - Global Scale")]
    [Tooltip("Arrow length representing the weight of one 25 g unit, in metres. " +
             "This is the single global scale - every arrow in every question " +
             "uses it. Shrink this to shorten all arrows at once; never scale " +
             "an individual arrow to fit the layout.")]
    public float unitArrowLength = 0.025f;

    [Header("Force Aid - Geometry")]
    public float arrowLineWidth = 0.003f;
    public float arrowHeadSize = 0.012f;
    public float arrowLabelFontSize = 2f;
    public float arrowLabelSideOffset = 0.035f;
    public string tensionSymbol = "T";
    public string weightSymbol = "W";
    [Tooltip("Off: the W arrow starts at the same point as that object's T " +
             "arrow. On: it starts at the bottom face of the lowest weight.")]
    public bool weightArrowFromChainBottom = false;
    [Tooltip("Force arrows always point straight up or straight down. Off: " +
             "tension arrows follow the rope, which tilts with wherever the " +
             "rope happened to settle.")]
    public bool forceArrowsAxisAligned = true;
    [Tooltip("Sideways offset of arrows drawn directly on a hook.")]
    public float hookSideOffset = 0.03f;

    [Header("Force Aid - Stack Placement")]
    [Tooltip("Horizontal distance from the movable pulley to the stacked column.")]
    public float stackHorizontalOffset = 0.12f;
    [Tooltip("Horizontal offset of the free-end arrow group.")]
    public float freeEndHorizontalOffset = 0.10f;
    public float overlapPushStep = 0.06f;
    public int maxOverlapPushes = 2;
    [Tooltip("Minimum horizontal separation between the two arrow groups.")]
    public float minGroupSeparation = 0.18f;
    [Tooltip("World Y above which the stack is considered out of comfortable view.")]
    public float comfortCeilingY = 1.75f;

    [Header("Force Aid - Comparison Stage")]
    [Tooltip("After the terminal state, bring both force groups onto one " +
             "shared baseline, close together and in front of everything, so " +
             "their lengths can actually be compared.")]
    public bool playComparisonStage = true;
    public float comparisonMoveDuration = 0.9f;
    [Tooltip("Pause on the terminal state before the comparison step starts.")]
    public float comparisonDelay = 1.2f;
    [Tooltip("Horizontal gap between the two groups on the comparison baseline.")]
    public float comparisonSpacing = 0.14f;
    [Tooltip("Height of the comparison baseline above the table surface.")]
    public float comparisonBaselineHeight = 0.45f;
    [Tooltip("Extra nudge toward the viewer for the comparison layout.")]
    public float comparisonForwardOffset = 0.18f;
    [Tooltip("Last-resort manual flip if the two groups still land on the " +
             "wrong sides in the comparison layout.")]
    public bool invertComparisonOrder = false;
    [Tooltip("Draw a dashed datum line through the comparison baseline. " +
             "Without it there is nothing for the eye to measure the arrows " +
             "against, and T and W appear to start from nowhere.")]
    public bool showComparisonBaseline = true;
    public Color comparisonBaselineColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public float comparisonBaselineWidth = 0.0015f;
    [Tooltip("How far the datum line extends past the outermost arrow.")]
    public float comparisonBaselineMargin = 0.05f;
    [Tooltip("Rule off the comparison layout in units of one rope's T, above " +
             "and below the baseline. Without a scale the eye cannot tell " +
             "whether the T stack is taller than W or merely looks it.")]
    public bool showComparisonGrid = true;
    public Color comparisonGridColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    public float comparisonGridWidth = 0.0008f;
    public int maxComparisonGridLines = 8;

    [Header("Force Aid - Leader Lines")]
    [Tooltip("Neutral grey: these lines describe motion and provenance, not " +
             "force, so they must not reuse the tension colour.")]
    public Color leaderLineColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    public float leaderLineWidth = 0.0012f;
    public float leaderDashLength = 0.008f;
    public float leaderGapLength = 0.006f;
    public int leaderMaxDashes = 40;

    // ── Inspector: track aid ──────────────────────────────────────────────

    [Header("Displacement Track Aid")]
    public float trackSegmentLength = 0.05f;
    public float trackGapRatio = 0.15f;
    public int trackMaxSegments = 12;
    public float trackLineWidth = 0.004f;
    public float trackStartTickLength = 0.03f;
    public float trackLateralOffset = 0.06f;
    [Tooltip("0 = tracks vanish the instant the student lets go. Raise this " +
             "if Round B shows students cannot count the segments in time.")]
    public float trackFadeOutDelay = 0f;

    // ── Inspector: formula panel ──────────────────────────────────────────

    [Header("Formula Panel")]
    [Tooltip("Font size of the formulas. The panel sizes itself to fit.")]
    public float formulaFontSize = 0.2f;
    [Tooltip("Legend and explanation text, as a fraction of the formula font size.")]
    [Range(0.4f, 1f)] public float formulaExplanationFontScale = 0.75f;
    [Tooltip("Toggle button text, as a fraction of the formula font size.")]
    [Range(0.4f, 1.5f)] public float formulaButtonFontScale = 0.9f;
    [Tooltip("Font size of the optional [1]/[2] markers.")]
    public float formulaMarkerFontSize = 1.5f;
    [Tooltip("Corner radius of the formula panel and its button, in metres. " +
             "Fixed physical size, so it no longer grows with the panel.")]
    public float formulaCornerRadiusMeters = 0.012f;
    [Tooltip("Position of the toggle button's top-left corner relative to the pulley.")]
    public Vector3 formulaPanelOffset = new Vector3(0.22f, 0.10f, 0f);
    [Tooltip("On: the formula body opens ABOVE the button, so the button keeps " +
             "its height and the panel never sinks below the table. " +
             "Off: the body opens below the button.")]
    public bool formulaOpensUpward = true;
    [Tooltip("Small [1] / [2] tags next to each tracked object. Off by default.")]
    public bool showFormulaMarkers = false;
    [Tooltip("Flip the panel 180 degrees if you are reading it from behind. " +
             "The panel faces along towardViewer; this inverts that.")]
    public bool flipFormulaPanelFacing = false;
    [Tooltip("Manual rotation applied on top of the viewer-facing orientation, " +
             "in degrees. Y yaws the panel left/right, X tilts the top toward " +
             "or away from the reader, Z rolls it. Safe to tune during Play.")]
    public Vector3 formulaPanelRotation = Vector3.zero;
    [Tooltip("Shared look for every panel in the scene. The formula panel takes " +
             "its colours and padding from here, but uses its own corner radius " +
             "and no border.")]
    public PanelStyle panelStyle;
    [Tooltip("Layer NAME the quiz ray can hit, so the toggle button works. " +
             "Must match QuizRaySelector's Quiz UI Layer.")]
    public string formulaUILayerName = "QuizUI";
    [Tooltip("What state the formula panel opens in. Collapsed by default: " +
             "a long formula left permanently on screen was reported as adding " +
             "cognitive load rather than relieving it.")]
    public FormulaPanel.PanelState formulaInitialState = FormulaPanel.PanelState.Collapsed;
    [Tooltip("Render queue of the formula panel. Below 3000 = drawn before the " +
             "quiz paper (UGUI canvas), so the quiz paper covers the panel. " +
             "Set before entering Play.")]
    public int formulaRenderQueue = 2900;
    [Tooltip("All wording on the formula panel: button labels, legend, explanations.")]
    public FormulaPanelTexts formulaTexts = new FormulaPanelTexts();

    public string markerTagLoad = "[1]";
    public string markerTagFree = "[2]";
    public Color formulaColor = Color.white;

    [Header("Debug")]
    public bool verboseLogging = false;
    [Tooltip("Print the once-per-second 'BLOCKED' gate report. Very noisy; " +
             "only needed when the force aid refuses to appear.")]
    public bool logGateState = false;

    // ── Internal state ────────────────────────────────────────────────────

    private enum ForceState { Idle, FadingIn, Stage1Hold, Stage2Merge, Terminal, FadingOut }

    private class ArrowEntry
    {
        public ForceArrowVisual visual;
        public Vector3 stageStart;
        public Vector3 terminalStart;
        public Vector3 comparisonStart;
        public bool moves;
        public int slot;
        public bool pointsUp;
    }

    private bool aidEnabled = false;
    private string currentAidType = null;
    private DifficultySetting lastSeenDifficulty = null;

    private ForceState forceState = ForceState.Idle;
    private float restTimer = 0f;
    private bool warnedNoWeights = false;
    private bool jumpToComparison = false;
    private bool comparisonSettled = false;
    private bool frozenTerminal = false;

    private Transform slot0Ref;
    private Transform slot1Ref;
    private float nextGateLogTime = 0f;
    private float nextPanelLogTime = 0f;
    private bool hasPlayedFullMerge = false;
    private float lastForceSignature = float.NaN;
    private bool currentIsMA2 = false;
    private bool externalRevealActive = false;
    private Coroutine forceCoroutine;

    private Transform aidRoot;
    private readonly List<ForceArrowVisual> tensionPool = new List<ForceArrowVisual>();
    private readonly List<ForceArrowVisual> weightPool = new List<ForceArrowVisual>();
    private int tensionUsed = 0;
    private int weightUsed = 0;
    private readonly List<ArrowEntry> activeArrows = new List<ArrowEntry>();

    private DashedLineVisual leaderA;
    private DashedLineVisual leaderB;
    private DashedLineVisual leaderGroup;
    private DashedLineVisual comparisonBaseline;
    private readonly List<DashedLineVisual> comparisonGrid = new List<DashedLineVisual>();
    private float comparisonUnitLength = 0f;
    private Vector3 comparisonBaseOrigin;
    private float comparisonHalfWidth;
    private float comparisonUpExtent;
    private float comparisonDownExtent;
    private Vector3 leaderAFrom, leaderATo;
    private Vector3 leaderBFrom, leaderBTo;
    private bool groupShiftedDown = false;
    private bool hasStackLeaders = false;
    private Vector3 groupShiftAnchor;

    private SegmentedTrackVisual trackLoad;
    private SegmentedTrackVisual trackFree;
    private bool tracksActive = false;
    private Transform trackedLoad;
    private Transform trackedFree;
    private float trackLoadStartY;
    private float trackFreeStartY;
    private Coroutine trackFadeCoroutine;

    private FormulaPanel formulaPanel;
    private bool formulaShown = false;
    private bool formulaShownAsMA2 = false;

    private RopeGrab ropeGrabL;
    private RopeGrab ropeGrabR;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    void Start()
    {
        PhysicsConstants.UnitArrowLength = unitArrowLength;
        if (mainCamera == null) mainCamera = Camera.main;

        if (hookL != null) ropeGrabL = hookL.GetComponent<RopeGrab>();
        if (hookR != null) ropeGrabR = hookR.GetComponent<RopeGrab>();

        GameObject rootObj = new GameObject("ConceptualAidVisuals");
        rootObj.transform.SetParent(transform, false);
        aidRoot = rootObj.transform;

        leaderA = new DashedLineVisual(aidRoot, "Leader_A", leaderLineColor,
            leaderLineWidth, leaderDashLength, leaderGapLength, leaderMaxDashes, renderOnTop);
        leaderB = new DashedLineVisual(aidRoot, "Leader_B", leaderLineColor,
            leaderLineWidth, leaderDashLength, leaderGapLength, leaderMaxDashes, renderOnTop);
        leaderGroup = new DashedLineVisual(aidRoot, "Leader_Group", leaderLineColor,
            leaderLineWidth, leaderDashLength, leaderGapLength, leaderMaxDashes, renderOnTop);
        comparisonBaseline = new DashedLineVisual(aidRoot, "ComparisonBaseline",
            comparisonBaselineColor, comparisonBaselineWidth,
            leaderDashLength, leaderGapLength, 80, renderOnTop);

        for (int i = 0; i < maxComparisonGridLines * 2; i++)
        {
            comparisonGrid.Add(new DashedLineVisual(aidRoot, "CompGrid_" + i,
                comparisonGridColor, comparisonGridWidth,
                leaderDashLength * 0.6f, leaderGapLength * 1.4f, 80, renderOnTop));
        }

        trackLoad = new SegmentedTrackVisual(aidRoot, "Track_Load", PhysicsConstants.TrackColor,
            trackLineWidth, trackSegmentLength, trackGapRatio, trackMaxSegments,
            trackStartTickLength, arrowPlaneNormal, renderOnTop);
        trackFree = new SegmentedTrackVisual(aidRoot, "Track_Free", PhysicsConstants.TrackColor,
            trackLineWidth, trackSegmentLength, trackGapRatio, trackMaxSegments,
            trackStartTickLength, arrowPlaneNormal, renderOnTop);

        formulaPanel = new FormulaPanel(aidRoot, "FormulaPanel",
            formulaFontSize, formulaExplanationFontScale, formulaButtonFontScale,
            formulaMarkerFontSize, formulaColor, markerTagLoad, markerTagFree,
            arrowPlaneNormal, showFormulaMarkers, renderOnTop,
            panelStyle, formulaUILayerName, formulaCornerRadiusMeters,
            formulaTexts, formulaRenderQueue);

        SetAidEnabled(false);
    }

    void Update()
    {
        DetectQuestionChange();

        if (pulleyPhysics == null) return;

        UpdateForceAid();
        UpdateTrackAid();
        UpdateFormulaPanel();
    }

    // ── Public API (called by QuizManager / ExperimentConfigManager / Play) ─

    /// <summary>Called by QuizManager.DisplayQuestion on every question change.</summary>
    public void SetCurrentAidType(string aidType)
    {
        if (currentAidType == aidType) return;
        currentAidType = aidType;
        ResetAllAids();
        if (verboseLogging) Debug.Log("[ConceptualAid] aidType = " + aidType);
    }

    /// <summary>Hooked to FidelityManager.onFidelityLevelChanged in the Inspector.</summary>
    public void OnFidelityLevelChanged(int newLevel)
    {
        // Three-level ladder: aid is on at Level 1 and Level 0, off at Level 2.
        SetAidEnabled(newLevel == 0 || newLevel == 1);
    }

    public void SetAidEnabled(bool enabled)
    {
        if (aidEnabled == enabled) return;
        aidEnabled = enabled;
        if (!enabled) ResetAllAids();
        if (verboseLogging) Debug.Log("[ConceptualAid] " + (enabled ? "ON" : "OFF"));
    }

    public bool IsAidEnabled() { return aidEnabled; }

    /// <summary>
    /// Entry point used by ExperimentConfigManager during a demo sequence.
    ///
    /// This has to do real work, not just self-triggered rest detection: a demo
    /// hangs the weights, shows them, and then retracts them again, so the only
    /// window in which forces actually exist is the one ECM hands us here. By
    /// the time the demo has finished and the system is "at rest", the weight
    /// chains are already null and every force is zero.
    /// </summary>
    public IEnumerator PlayForceReveal(string aidType)
    {
        if (!aidEnabled || aidType != "force") yield break;
        if (pulleyPhysics == null) yield break;

        externalRevealActive = true;

        // ECM calls this the moment the weights are attached, before anything
        // has settled. Wait for the system to actually stop, so the arrows are
        // sampled and drawn at the resting position rather than the drop point.
        float waited = 0f;
        float restHeld = 0f;
        while (waited < revealSettleTimeout)
        {
            waited += Time.deltaTime;
            if (Mathf.Abs(pulleyPhysics.velocity) < restVelocityThreshold)
                restHeld += Time.deltaTime;
            else
                restHeld = 0f;

            if (restHeld >= revealSettleHoldTime) break;
            yield return null;
        }

        StopForceCoroutine();
        BuildForceArrows();

        if (activeArrows.Count == 0)
        {
            externalRevealActive = false;
            yield break;
        }

        yield return ForceSequence(!hasPlayedFullMerge);

        // Hold the terminal state so it stays readable before the demo
        // retracts the equipment.
        float hold = 0f;
        while (hold < demoTerminalHoldTime)
        {
            hold += Time.deltaTime;
            yield return null;
        }

        externalRevealActive = false;

        // Retracting the equipment zeroes every weight chain, which would
        // otherwise trigger a rebuild with no forces in it and wipe the
        // diagram the student is still reading.
        if (holdDiagramAfterDemo)
        {
            frozenTerminal = true;
            if (verboseLogging)
                Debug.Log("[ConceptualAid] Diagram frozen in comparison layout");
        }
    }

    /// <summary>
    /// True only when pressing Play would actually replay something.
    ///
    /// Deliberately strict: 1-1 has no merge animation to replay, and the
    /// formula panel only has a second step on 3-2. Returning true in those
    /// cases left the Play button floating with nothing behind it.
    /// </summary>
    public bool CanReplay()
    {
        if (!aidEnabled) return false;

        // Only force questions have anything to replay. "distance" and
        // "velocity" questions show displacement tracks and a formula panel,
        // neither of which is an animation - offering a replay button there
        // would label a button with something it does not do.
        if (currentAidType != "force") return false;
        if (forceState != ForceState.Terminal) return false;

        // Terminal is reached when the merge finishes, but the comparison stage
        // runs on after that. Offering a replay mid-animation lets the student
        // restart the thing they have not finished watching.
        if (playComparisonStage && !comparisonSettled) return false;

        return true;
    }

    /// <summary>Called by DemoPlayButtonController when Play is pressed.</summary>
    public void OnReplayRequested()
    {
        if (!aidEnabled) return;

        if (currentAidType == "force")
        {
            frozenTerminal = false;
            // Replay re-places the equipment first (DemoPlayButtonController
            // drives that), so the rebuild reads live masses off real weights.
            // The diagram and the apparatus therefore always agree.
            hasPlayedFullMerge = false;
            StopForceCoroutine();
            ClearForceArrows();
            forceState = ForceState.Idle;
            restTimer = restHoldTime; // fire on the next frame if still at rest
        }
    }

    /// <summary>Reserved for gaze-triggered replay (Round C). Not wired yet.</summary>
    public void OnGazeDwellOnStack()
    {
        OnReplayRequested();
    }

    // ── Question change ───────────────────────────────────────────────────

    private void DetectQuestionChange()
    {
        if (quizManager == null) return;
        DifficultySetting current = quizManager.GetCurrentDifficultySettingPublic();
        if (current == lastSeenDifficulty) return;
        lastSeenDifficulty = current;

        // Read aidType straight off the question rather than waiting for
        // QuizManager to push it in. A missing Inspector reference on
        // QuizManager used to leave currentAidType null, which silently killed
        // the self-triggered force aid while the ECM-driven path kept working
        // (it receives aidType as an argument).
        string aid = current != null ? current.aidType : null;
        if (currentAidType != aid)
        {
            currentAidType = aid;
            if (verboseLogging)
                Debug.Log("[ConceptualAid] aidType resolved from question: " + aid);
        }

        ResetAllAids();
    }

    private void ResetAllAids()
    {
        hasPlayedFullMerge = false;
        externalRevealActive = false;
        lastForceSignature = float.NaN;
        warnedNoWeights = false;
        jumpToComparison = false;
        comparisonSettled = false;
        frozenTerminal = false;
        StopForceCoroutine();
        ClearForceArrows();
        forceState = ForceState.Idle;
        restTimer = 0f;
        EndTracksImmediate();
        if (formulaPanel != null) formulaPanel.Hide();
        formulaShown = false;
        formulaShownAsMA2 = false;
    }

    // ── Force aid ─────────────────────────────────────────────────────────

    private void UpdateForceAid()
    {
        // While ExperimentConfigManager is driving the reveal, stay out of the
        // way. ECM.IsPlaying is true for the whole demo, so the gate below
        // would otherwise fade the arrows out the instant they appeared.
        //
        // Watchdog: if ECM stopped its demo coroutine the nested reveal dies
        // with it and never clears the flag, which would leave the force aid
        // permanently frozen for the rest of the question.
        if (externalRevealActive && !IsEquipmentAnimationPlaying())
        {
            externalRevealActive = false;
            if (verboseLogging)
                Debug.Log("[ConceptualAid] External reveal flag cleared by watchdog");
        }
        if (externalRevealActive) return;

        // Frozen after a demo: hold the comparison layout until the student
        // does something, replays, or moves to another question.
        if (frozenTerminal)
        {
            if (AnyGrabActive())
            {
                frozenTerminal = false;
                lastForceSignature = float.NaN;
            }
            else
            {
                return;
            }
        }

        // A grab is a hard block: the readings are meaningless mid-manipulation
        // and the arrows would chase the hand. Everything else is a soft wait.
        bool aidActive = aidEnabled
                         && currentAidType == "force"
                         && !IsEquipmentAnimationPlaying()
                         && !AnyGrabActive();

        if (!aidActive || !IsSystemAtRest())
        {
            LogGateState();
            restTimer = 0f;
            if (forceState != ForceState.Idle && forceState != ForceState.FadingOut)
                BeginForceFadeOut();
            return;
        }

        // Rebuild as soon as the load changes. Waiting for the next motion
        // event or a Play press leaves stale arrows on screen while the
        // student is still adding weights.
        if (forceState == ForceState.Terminal)
        {
            float signature = ForceSignature();
            if (!float.IsNaN(lastForceSignature)
                && Mathf.Abs(signature - lastForceSignature) > 0.0001f)
            {
                if (verboseLogging)
                    Debug.Log("[ConceptualAid] Load changed - rebuilding force arrows");
                StopForceCoroutine();
                ClearForceArrows();
                forceState = ForceState.Idle;
                restTimer = 0f;
                // Adding a weight does not always restart the motion - one end
                // can already be resting against its stop - so the rebuild is
                // driven by the load itself, not by a motion event. It still
                // waits for rest before showing, and it goes straight to the
                // comparison layout: no point replaying where the arrows came
                // from every time a weight is added.
                jumpToComparison = true;
                return;
            }
        }

        if (forceState != ForceState.Idle) return;

        restTimer += Time.deltaTime;
        if (restTimer < restHoldTime) return;

        restTimer = 0f;
        if (verboseLogging)
            Debug.Log("[ConceptualAid] Rest confirmed at t=" + Time.time.ToString("F2")
                      + " velocity=" + pulleyPhysics.velocity.ToString("F4")
                      + " -> starting reveal");
        StartForceSequence(!hasPlayedFullMerge);
    }

    /// <summary>
    /// Print, at most once a second, exactly which gate condition is holding
    /// the force aid shut. Guessing from the absence of a log is slow; this
    /// names the blocker directly.
    /// </summary>
    private void LogGateState()
    {
        if (!verboseLogging || !logGateState) return;
        if (Time.time < nextGateLogTime) return;
        nextGateLogTime = Time.time + 1f;

        bool gl = ropeGrabL != null && ropeGrabL.isGrabbed;
        bool gr = ropeGrabR != null && ropeGrabR.isGrabbed;
        bool loadGrab = pulleyPhysics != null && pulleyPhysics.IsLoadGrabbed();
        float v = pulleyPhysics != null ? pulleyPhysics.velocity : 0f;

        Debug.Log("[ConceptualAid] BLOCKED"
            + " | aidEnabled=" + aidEnabled
            + " aidType=" + (currentAidType ?? "null")
            + " ecmPlaying=" + IsEquipmentAnimationPlaying()
            + " grabL=" + gl + " (ref " + (ropeGrabL != null) + ")"
            + " grabR=" + gr + " (ref " + (ropeGrabR != null) + ")"
            + " loadGrabbed=" + loadGrab
            + " velocity=" + v.ToString("F4")
            + " threshold=" + restVelocityThreshold
            + " state=" + forceState);
    }

    /// <summary>
    /// Does the current question call for a movable pulley? Taken from the
    /// question's own equipment configuration, so it is true from the moment
    /// the question opens - before anything has been placed in the scene.
    /// </summary>
    private bool QuestionUsesMovablePulley()
    {
        DifficultySetting d = lastSeenDifficulty;
        if (d == null && quizManager != null)
            d = quizManager.GetCurrentDifficultySettingPublic();
        if (d == null) return pulleyPhysics != null && pulleyPhysics.IsMovablePulleyConfig();

        if (d.experimentConfigs != null)
        {
            for (int i = 0; i < d.experimentConfigs.Length; i++)
                if (d.experimentConfigs[i] != null && d.experimentConfigs[i].useMovablePulley)
                    return true;
        }

        if (d.demoSequences != null)
        {
            for (int i = 0; i < d.demoSequences.Length; i++)
                if (d.demoSequences[i] != null && d.demoSequences[i].useMovablePulley)
                    return true;
        }

        return false;
    }

    private bool IsEquipmentAnimationPlaying()
    {
        return experimentConfigManager != null && experimentConfigManager.IsPlaying;
    }

    private bool IsSystemAtRest()
    {
        if (pulleyPhysics == null) return false;
        return Mathf.Abs(pulleyPhysics.velocity) < restVelocityThreshold;
    }

    private bool AnyGrabActive()
    {
        if (ropeGrabL != null && ropeGrabL.isGrabbed) return true;
        if (ropeGrabR != null && ropeGrabR.isGrabbed) return true;
        if (pulleyPhysics != null && pulleyPhysics.IsLoadGrabbed()) return true;
        return false;
    }

    private void StartForceSequence(bool playFullAnimation)
    {
        StopForceCoroutine();
        BuildForceArrows();
        if (activeArrows.Count == 0)
        {
            forceState = ForceState.Idle;
            return;
        }
        forceCoroutine = StartCoroutine(ForceSequence(playFullAnimation));
    }

    private void StopForceCoroutine()
    {
        if (forceCoroutine != null)
        {
            StopCoroutine(forceCoroutine);
            forceCoroutine = null;
        }
    }

    private IEnumerator ForceSequence(bool playFullAnimation)
    {
        // The comparison layout is the resting state of this aid, so every
        // reveal ends there. Only the provenance part - the in-place arrows,
        // and for the movable pulley the three-stage merge - is shown once per
        // question. After that, and on any rebuild, go straight to the
        // comparison layout and just fade in: replaying "here is where each
        // force came from" on every demo step or every added weight is noise.
        bool straightToComparison = jumpToComparison || !playFullAnimation;
        jumpToComparison = false;

        if (straightToComparison)
        {
            BuildComparisonLayout();
            for (int i = 0; i < activeArrows.Count; i++)
                activeArrows[i].visual.SetStartPoint(activeArrows[i].comparisonStart);

            if (leaderA != null) leaderA.SetVisible(false);
            if (leaderB != null) leaderB.SetVisible(false);
            if (leaderGroup != null) leaderGroup.SetVisible(false);
            ShowComparisonBaseline();
            comparisonSettled = true;
        }

        forceState = ForceState.FadingIn;
        SetForceAlpha(0f);
        SetForceVisible(true);

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            SetForceAlpha(Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        SetForceAlpha(1f);

        if (straightToComparison)
        {
            forceState = ForceState.Terminal;
            hasPlayedFullMerge = true;
            forceCoroutine = null;
            if (verboseLogging)
                Debug.Log("[ConceptualAid] Revealed directly in comparison layout");
            yield break;
        }

        // 1-1: no merge animation, but it still needs the comparison step -
        // the two sides are metres apart, which is exactly the case the
        // comparison layout exists to solve.
        if (!currentIsMA2)
        {
            forceState = ForceState.Terminal;
            if (playComparisonStage)
                yield return ComparisonStage();
            hasPlayedFullMerge = true;
            forceCoroutine = null;
            yield break;
        }

        if (playFullAnimation)
        {
            forceState = ForceState.Stage1Hold;
            float hold = 0f;
            while (hold < stage1HoldDuration)
            {
                hold += Time.deltaTime;
                yield return null;
            }

            forceState = ForceState.Stage2Merge;
            float m = 0f;
            while (m < stage2MergeDuration)
            {
                m += Time.deltaTime;
                float k = Mathf.Clamp01(m / stage2MergeDuration);
                k = k * k * (3f - 2f * k); // ease-in-out
                ApplyMergeProgress(k);
                yield return null;
            }
            ApplyMergeProgress(1f);
            hasPlayedFullMerge = true;
        }
        else
        {
            // Already seen the full animation on this question: land on the
            // terminal state directly instead of forcing a 2.8 s replay every
            // time the student stops moving.
            ApplyMergeProgress(1f);
        }

        ShowLeaderLines();
        forceState = ForceState.Terminal;

        if (playComparisonStage)
            yield return ComparisonStage();

        forceCoroutine = null;
    }

    /// <summary>
    /// Final step: bring both groups onto one shared baseline, close together
    /// and in front of the apparatus. Two arrows metres apart on opposite sides
    /// of the rig cannot be compared by eye; side by side on a common baseline
    /// they can. Translation only - lengths are untouched.
    /// </summary>
    private IEnumerator ComparisonStage()
    {
        float wait = 0f;
        while (wait < comparisonDelay)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        BuildComparisonLayout();

        // Leader lines describe the earlier merge, not this layout.
        if (leaderA != null) leaderA.SetVisible(false);
        if (leaderB != null) leaderB.SetVisible(false);
        if (leaderGroup != null) leaderGroup.SetVisible(false);

        for (int i = 0; i < activeArrows.Count; i++)
            activeArrows[i].terminalStart = activeArrows[i].visual.StartPoint;

        float t = 0f;
        while (t < comparisonMoveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / comparisonMoveDuration);
            k = k * k * (3f - 2f * k);
            for (int i = 0; i < activeArrows.Count; i++)
            {
                ArrowEntry e = activeArrows[i];
                e.visual.SetStartPoint(Vector3.Lerp(e.terminalStart, e.comparisonStart, k));
            }
            yield return null;
        }

        for (int i = 0; i < activeArrows.Count; i++)
            activeArrows[i].visual.SetStartPoint(activeArrows[i].comparisonStart);

        ShowComparisonBaseline();
        comparisonSettled = true;
    }

    /// <summary>
    /// The datum line every arrow in the comparison layout starts from.
    /// </summary>
    private void ShowComparisonBaseline()
    {
        if (!showComparisonBaseline || comparisonBaseline == null) return;
        Vector3 side = PlaneSide();
        comparisonBaseline.SetLine(
            comparisonBaseOrigin - side * comparisonHalfWidth,
            comparisonBaseOrigin + side * comparisonHalfWidth);
        comparisonBaseline.SetAlpha(1f);

        ShowComparisonGrid();
    }

    private void BuildComparisonLayout()
    {
        Vector3 side = PlaneSide();
        Vector3 forward = towardViewer.sqrMagnitude < 0.0001f
            ? Vector3.zero : towardViewer.normalized;

        Transform anchor = fixedPulley != null ? fixedPulley : movablePulley;
        Vector3 centre = anchor != null ? anchor.position : transform.position;

        Vector3 baseOrigin = new Vector3(centre.x, minWorldY + comparisonBaselineHeight, centre.z)
                             + forward * (aidForwardOffset + comparisonForwardOffset);
        comparisonBaseOrigin = baseOrigin;
        comparisonHalfWidth = comparisonSpacing * 0.5f + comparisonBaselineMargin;
        comparisonUpExtent = 0f;
        comparisonDownExtent = 0f;

        // Two slots, centred on the anchor, so the groups sit next to each
        // other rather than at their original separation.
        // Resolve each slot's side from where its group actually sits, the
        // same way the in-place layout does. A hard-coded sign here mirrored
        // the whole comparison whenever arrowPlaneNormal pointed the other way.
        float sign0 = SlotSign(slot0Ref, centre, side);
        float sign1 = SlotSign(slot1Ref, centre, side);
        if (Mathf.Approximately(sign0, sign1)) sign1 = -sign0;
        if (invertComparisonOrder) { sign0 = -sign0; sign1 = -sign1; }

        float[] upCursor = new float[2];
        float[] downCursor = new float[2];

        for (int i = 0; i < activeArrows.Count; i++)
        {
            ArrowEntry e = activeArrows[i];
            int slot = Mathf.Clamp(e.slot, 0, 1);
            float lateral = (slot == 0 ? sign0 : sign1) * 0.5f * comparisonSpacing;
            Vector3 slotBase = baseOrigin + side * lateral;

            if (e.pointsUp)
            {
                e.comparisonStart = slotBase + Vector3.up * upCursor[slot];
                upCursor[slot] += e.visual.Length;
                comparisonUpExtent = Mathf.Max(comparisonUpExtent, upCursor[slot]);
            }
            else
            {
                e.comparisonStart = slotBase - Vector3.up * downCursor[slot];
                downCursor[slot] += e.visual.Length;
                comparisonDownExtent = Mathf.Max(comparisonDownExtent, downCursor[slot]);
            }
        }
    }

    /// <summary>
    /// Rule off the comparison layout in units of one rope's tension, above and
    /// below the datum. This is what turns "the stack looks taller" into
    /// "the stack is two units and the weight is one and a half".
    /// </summary>
    private void ShowComparisonGrid()
    {
        if (comparisonGrid.Count == 0) return;

        for (int i = 0; i < comparisonGrid.Count; i++)
            comparisonGrid[i].SetVisible(false);

        if (!showComparisonGrid) return;
        if (comparisonUnitLength < 0.0005f) return;

        Vector3 side = PlaneSide();
        float halfWidth = comparisonHalfWidth;
        int used = 0;

        int upCount = Mathf.Min(maxComparisonGridLines,
            Mathf.CeilToInt(comparisonUpExtent / comparisonUnitLength));
        int downCount = Mathf.Min(maxComparisonGridLines,
            Mathf.CeilToInt(comparisonDownExtent / comparisonUnitLength));

        for (int k = 1; k <= upCount && used < comparisonGrid.Count; k++, used++)
        {
            Vector3 c = comparisonBaseOrigin + Vector3.up * (comparisonUnitLength * k);
            comparisonGrid[used].SetLine(c - side * halfWidth, c + side * halfWidth);
            comparisonGrid[used].SetAlpha(1f);
        }

        for (int k = 1; k <= downCount && used < comparisonGrid.Count; k++, used++)
        {
            Vector3 c = comparisonBaseOrigin - Vector3.up * (comparisonUnitLength * k);
            comparisonGrid[used].SetLine(c - side * halfWidth, c + side * halfWidth);
            comparisonGrid[used].SetAlpha(1f);
        }
    }

    private void BeginForceFadeOut()
    {
        StopForceCoroutine();
        forceCoroutine = StartCoroutine(ForceFadeOut());
    }

    private IEnumerator ForceFadeOut()
    {
        forceState = ForceState.FadingOut;
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            SetForceAlpha(1f - Mathf.Clamp01(t / fadeOutDuration));
            yield return null;
        }
        ClearForceArrows();
        forceState = ForceState.Idle;
        forceCoroutine = null;
    }

    // ── Force arrow construction ──────────────────────────────────────────

    /// <summary>
    /// Cheap fingerprint of the current load. The multipliers keep it from
    /// collapsing when equal masses move between hooks.
    /// </summary>
    private float ForceSignature()
    {
        if (pulleyPhysics == null) return 0f;
        return ChainMass(pulleyPhysics.weightChainLeft) * 1f
             + ChainMass(pulleyPhysics.weightChainRight) * 7f
             + ChainMass(pulleyPhysics.weightChainLoad) * 13f
             + ChainMass(pulleyPhysics.weightChainForce) * 29f;
    }

    private void BuildForceArrows()
    {
        // Re-apply so the scale can be tuned live in the Inspector during Play.
        PhysicsConstants.UnitArrowLength = unitArrowLength;
        ClearForceArrows();
        currentIsMA2 = pulleyPhysics.IsMovablePulleyConfig();

        if (currentIsMA2) BuildMovablePulleyArrows();
        else BuildAtwoodArrows();

        lastForceSignature = ForceSignature();
    }

    /// <summary>1-1: two T up along the rope, two W down. All four equal.</summary>
    private void BuildAtwoodArrows()
    {
        Vector3 side = PlaneSide();

        // Take the hook transforms from PulleyPhysics, not from this
        // component's own hookL / hookR fields. WeightSnap keys weightChainLeft
        // and weightChainRight strictly off pulleyPhysics.hookLeft / hookRight,
        // so reading them from anywhere else means a crossed Inspector
        // reference silently swaps the two sides' weights.
        Transform hL = pulleyPhysics.hookLeft != null ? pulleyPhysics.hookLeft : hookL;
        Transform hR = pulleyPhysics.hookRight != null ? pulleyPhysics.hookRight : hookR;

        // The comparison layout has to reach the same left/right verdict as the
        // in-place layout, so both are derived from these objects' real
        // positions rather than from a hard-coded sign.
        slot0Ref = hL;
        slot1Ref = hR;

        // Compute the chain masses here rather than reading pulleyPhysics.massLeft /
        // massRight. Those are cached in FixedUpdate, and a demo sequence calls
        // PlayForceReveal in the same frame the weights are attached - so the
        // cached values are still a frame behind and read as zero.
        float massL = ChainMass(pulleyPhysics.weightChainLeft);
        float massR = ChainMass(pulleyPhysics.weightChainRight);
        float liveTension = pulleyPhysics.tensionForce;

        float lenL = PhysicsConstants.MassToLength(massL);
        float lenR = PhysicsConstants.MassToLength(massR);

        if (verboseLogging && (massL > 0.0001f || massR > 0.0001f))
        {
            Debug.Log("[ConceptualAid] Atwood arrows"
                + " | LEFT hook=" + (hL != null ? hL.name : "NULL")
                + " mass=" + massL
                + " chain=" + (pulleyPhysics.weightChainLeft != null ? pulleyPhysics.weightChainLeft.name : "NULL")
                + " || RIGHT hook=" + (hR != null ? hR.name : "NULL")
                + " mass=" + massR
                + " chain=" + (pulleyPhysics.weightChainRight != null ? pulleyPhysics.weightChainRight.name : "NULL"));
        }

        if (massL <= 0.0001f && massR <= 0.0001f)
        {
            if (!warnedNoWeights)
            {
                warnedNoWeights = true;
                Debug.LogWarning("[ConceptualAid] No weights on either hook yet - " +
                                 "nothing to draw. At high fidelity the student hangs " +
                                 "the weights themselves.");
            }
            return;
        }
        warnedNoWeights = false;

        // Tension is a property of the rope, not of one side's weight. Reading
        // it from the solver means the T arrows respond when the OTHER side
        // changes, instead of each side silently mirroring its own W.
        // The aid is only ever drawn at rest, and an unbalanced Atwood comes to
        // rest against a travel stop: the heavier side sits on the stop and the
        // lighter side hangs free, so the rope carries exactly the lighter
        // weight. Using the solver's dynamic tension instead gave a value
        // between the two weights, which made T match neither W and land off
        // the unit gridlines.
        float tension = Mathf.Min(massL, massR) * PhysicsConstants.G;
        if (tension <= 0.0001f && liveTension > 0.0001f) tension = liveTension;
        float lenT = PhysicsConstants.ForceToLength(tension);

        comparisonUnitLength = lenT;

        if (verboseLogging)
            Debug.Log("[ConceptualAid] Atwood tension=" + tension + "N -> " + lenT + "m");

        if (hL != null)
        {
            Vector3 outL = OutwardSide(hL);
            Vector3 originL = hL.position + outL * hookSideOffset;
            // No tension arrow when the rope carries no force. A zero-length
            // arrow still renders its head and label, which would claim a
            // force that is not there.
            if (lenT > 0.0005f)
            {
                Vector3 dirL = RopeDirection(hL, pulleyPhysics.slotLeft);
                ArrowEntry tL = AddTensionArrow(originL, dirL, lenT, false);
                tL.slot = 0; tL.pointsUp = true;
            }

            // No weight arrow when there is no weight. Tension and weight are
            // gated separately: either can legitimately be zero while the
            // other is not.
            if (lenL > 0.0005f)
            {
                Vector3 wOriginL = weightArrowFromChainBottom
                    ? ChainBottom(pulleyPhysics.weightChainLeft, hL) + outL * hookSideOffset
                    : originL;
                ArrowEntry wL = AddWeightArrow(wOriginL, Vector3.down, lenL, false);
                wL.slot = 0; wL.pointsUp = false;
            }
        }

        if (hR != null)
        {
            Vector3 outR = OutwardSide(hR);
            Vector3 originR = hR.position + outR * hookSideOffset;
            if (lenT > 0.0005f)
            {
                Vector3 dirR = RopeDirection(hR, pulleyPhysics.slotRight);
                ArrowEntry tR = AddTensionArrow(originR, dirR, lenT, false);
                tR.slot = 1; tR.pointsUp = true;
            }

            if (lenR > 0.0005f)
            {
                Vector3 wOriginR = weightArrowFromChainBottom
                    ? ChainBottom(pulleyPhysics.weightChainRight, hR) + outR * hookSideOffset
                    : originR;
                ArrowEntry wR = AddWeightArrow(wOriginR, Vector3.down, lenR, false);
                wR.slot = 1; wR.pointsUp = false;
            }
        }
    }

    /// <summary>2-1 / 2-2: stage-1 layout plus the merged terminal targets.</summary>
    private void BuildMovablePulleyArrows()
    {
        Vector3 side = PlaneSide();

        float loadMass = ChainMass(pulleyPhysics.weightChainLoad);
        float freeMass = ChainMass(pulleyPhysics.weightChainForce);

        // Sampled once, here, and locked until fade-out.
        float tension = freeMass * PhysicsConstants.G;
        float lenT = PhysicsConstants.ForceToLength(tension);
        float lenWLoad = PhysicsConstants.MassToLength(loadMass);
        float lenWFree = PhysicsConstants.MassToLength(freeMass);

        if (verboseLogging)
        {
            Debug.Log("[ConceptualAid] MA2 arrows: loadMass=" + loadMass + " freeMass=" + freeMass +
                      " | slotL=" + (movableRopeSlotL != null ? movableRopeSlotL.name : "NULL") +
                      " slotR=" + (movableRopeSlotR != null ? movableRopeSlotR.name : "NULL"));
        }

        comparisonUnitLength = lenT;

        // Tension and weight are gated separately here too. Returning early on
        // zero tension used to suppress the load's W arrow as well, so a
        // student who had hung weights on the movable pulley but not yet on the
        // free end saw nothing at all.
        bool drawTension = lenT > 0.0005f;
        if (!drawTension && lenWLoad < 0.0005f)
        {
            if (!warnedNoWeights)
            {
                warnedNoWeights = true;
                Debug.LogWarning("[ConceptualAid] Movable-pulley config has no weights " +
                                 "on either the pulley or the free end - nothing to draw.");
            }
            return;
        }
        warnedNoWeights = false;

        Transform slotML = movableRopeSlotL != null ? movableRopeSlotL : movablePulley;
        Transform slotMR = movableRopeSlotR != null ? movableRopeSlotR : movablePulley;
        Transform freeEnd = pulleyPhysics.GetFreeEndHook();
        if (freeEnd == null) freeEnd = hookR;

        slot0Ref = movablePulley;
        slot1Ref = freeEnd;

        // ── Stack baseline ──
        // Placed on the side of the movable pulley that faces away from the
        // free-end group, so the two groups never sit between each other and
        // the rope. Resolved once, at build time, so it does not flicker.
        Vector3 freeOut = freeEnd != null ? OutwardSide(freeEnd) : side;
        Vector3 stackSide = -freeOut;
        if (freeEnd != null)
        {
            float d = Vector3.Dot(movablePulley.position - freeEnd.position, side);
            stackSide = d >= 0f ? side : -side;
        }

        Vector3 stackBase = movablePulley.position + stackSide * stackHorizontalOffset;

        Vector3 freeGroupPos = freeEnd != null
            ? freeEnd.position + freeOut * freeEndHorizontalOffset
            : movablePulley.position - stackSide * 0.5f;

        // Push the whole group horizontally if it collides with the free-end
        // group. Fixed step, at most twice. Never compress the arrows.
        for (int i = 0; i < maxOverlapPushes; i++)
        {
            float separation = Mathf.Abs(Vector3.Dot(freeGroupPos - stackBase, side));
            if (separation >= minGroupSeparation) break;
            stackBase += stackSide * overlapPushStep;
        }

        // If the column top leaves the comfortable field of view, move the
        // whole group down and keep a short leader so it still belongs to the
        // pulley. Length always wins over framing.
        groupShiftedDown = false;
        float topY = stackBase.y + 2f * lenT;
        if (topY > comfortCeilingY)
        {
            stackBase.y -= (topY - comfortCeilingY);
            groupShiftedDown = true;
            groupShiftAnchor = movablePulley.position;
        }

        // ── Stage 1: arrows drawn along the rope ──
        ArrowEntry t1 = null;
        ArrowEntry t2 = null;
        if (drawTension)
        {
            t1 = AddTensionArrow(slotML.position, Vector3.up, lenT, true);
            t2 = AddTensionArrow(slotMR.position, Vector3.up, lenT, true);
            t1.slot = 0; t1.pointsUp = true;
            t2.slot = 0; t2.pointsUp = true;
        }

        ArrowEntry wLoad = null;
        if (lenWLoad > 0.0005f)
        {
            Vector3 loadOrigin = weightArrowFromChainBottom
                ? ChainBottom(pulleyPhysics.weightChainLoad, movablePulley)
                : movablePulley.position;
            wLoad = AddWeightArrow(loadOrigin, Vector3.down, lenWLoad, true);
            wLoad.slot = 0; wLoad.pointsUp = false;
        }

        if (freeEnd != null)
        {
            Vector3 freeOrigin = freeEnd.position + freeOut * freeEndHorizontalOffset;
            if (drawTension)
            {
                Vector3 dirFree = RopeDirection(freeEnd, pulleyPhysics.slotRight);
                ArrowEntry tFree = AddTensionArrow(freeOrigin, dirFree, lenT, false);
                tFree.slot = 1; tFree.pointsUp = true;
            }

            if (lenWFree > 0.0005f)
            {
                Vector3 wFreeOrigin = weightArrowFromChainBottom
                    ? ChainBottom(pulleyPhysics.weightChainForce, freeEnd) + freeOut * freeEndHorizontalOffset
                    : freeOrigin;
                ArrowEntry wFree = AddWeightArrow(wFreeOrigin, Vector3.down, lenWFree, false);
                wFree.slot = 1; wFree.pointsUp = false;
            }
        }

        // ── Stage 3 targets: head-to-tail column above the baseline, W below ──
        if (t1 != null) t1.terminalStart = stackBase;
        if (t2 != null) t2.terminalStart = stackBase + Vector3.up * lenT;
        if (wLoad != null) wLoad.terminalStart = stackBase;

        // Leader lines: from each stacked segment back to the rope segment it
        // came from. Without these the column cannot be traced to its source.
        hasStackLeaders = (t1 != null && t2 != null);
        if (hasStackLeaders)
        {
            leaderAFrom = t1.terminalStart + Vector3.up * (lenT * 0.5f);
            leaderATo = t1.stageStart + Vector3.up * (lenT * 0.5f);
            leaderBFrom = t2.terminalStart + Vector3.up * (lenT * 0.5f);
            leaderBTo = t2.stageStart + Vector3.up * (lenT * 0.5f);
        }
    }

    private void ApplyMergeProgress(float k)
    {
        for (int i = 0; i < activeArrows.Count; i++)
        {
            ArrowEntry e = activeArrows[i];
            if (!e.moves) continue;
            e.visual.SetStartPoint(Vector3.Lerp(e.stageStart, e.terminalStart, k));
        }
    }

    private void ShowLeaderLines()
    {
        if (!currentIsMA2) return;
        if (!hasStackLeaders) return;
        leaderA.SetLine(leaderAFrom, leaderATo);
        leaderB.SetLine(leaderBFrom, leaderBTo);
        leaderA.SetAlpha(1f);
        leaderB.SetAlpha(1f);

        if (groupShiftedDown)
        {
            leaderGroup.SetLine(groupShiftAnchor, leaderAFrom);
            leaderGroup.SetAlpha(1f);
        }
    }

    private ArrowEntry AddTensionArrow(Vector3 start, Vector3 dir, float length, bool moves)
    {
        start = PlaceArrow(start, dir, length);
        ForceArrowVisual v = GetTensionArrow();
        v.SetArrow(start, dir, length, tensionSymbol);
        ArrowEntry e = new ArrowEntry();
        e.visual = v;
        e.stageStart = start;
        e.terminalStart = start;
        e.moves = moves;
        activeArrows.Add(e);
        return e;
    }

    private ArrowEntry AddWeightArrow(Vector3 start, Vector3 dir, float length, bool moves)
    {
        start = PlaceArrow(start, dir, length);
        ForceArrowVisual v = GetWeightArrow();
        v.SetArrow(start, dir, length, weightSymbol);
        ArrowEntry e = new ArrowEntry();
        e.visual = v;
        e.stageStart = start;
        e.terminalStart = start;
        e.moves = moves;
        activeArrows.Add(e);
        return e;
    }

    private ForceArrowVisual GetTensionArrow()
    {
        if (tensionUsed >= tensionPool.Count)
        {
            tensionPool.Add(new ForceArrowVisual(aidRoot, "T_" + tensionPool.Count,
                PhysicsConstants.TensionColor, arrowLineWidth, arrowHeadSize,
                arrowLabelFontSize, arrowLabelSideOffset, arrowPlaneNormal, renderOnTop));
        }
        ForceArrowVisual v = tensionPool[tensionUsed];
        tensionUsed++;
        return v;
    }

    private ForceArrowVisual GetWeightArrow()
    {
        if (weightUsed >= weightPool.Count)
        {
            weightPool.Add(new ForceArrowVisual(aidRoot, "W_" + weightPool.Count,
                PhysicsConstants.WeightColor, arrowLineWidth, arrowHeadSize,
                arrowLabelFontSize, arrowLabelSideOffset, arrowPlaneNormal, renderOnTop));
        }
        ForceArrowVisual v = weightPool[weightUsed];
        weightUsed++;
        return v;
    }

    private void SetForceVisible(bool visible)
    {
        for (int i = 0; i < activeArrows.Count; i++)
            activeArrows[i].visual.SetVisible(visible);
    }

    private void SetForceAlpha(float a)
    {
        for (int i = 0; i < activeArrows.Count; i++)
            activeArrows[i].visual.SetAlpha(a);
        if (leaderA != null) leaderA.SetAlpha(a);
        if (leaderB != null) leaderB.SetAlpha(a);
        if (leaderGroup != null) leaderGroup.SetAlpha(a);
        if (comparisonBaseline != null) comparisonBaseline.SetAlpha(a);
        for (int i = 0; i < comparisonGrid.Count; i++) comparisonGrid[i].SetAlpha(a);
    }

    private void ClearForceArrows()
    {
        for (int i = 0; i < activeArrows.Count; i++)
            activeArrows[i].visual.SetVisible(false);
        activeArrows.Clear();
        tensionUsed = 0;
        weightUsed = 0;
        comparisonSettled = false;
        if (leaderA != null) leaderA.SetVisible(false);
        if (leaderB != null) leaderB.SetVisible(false);
        if (leaderGroup != null) leaderGroup.SetVisible(false);
        if (comparisonBaseline != null) comparisonBaseline.SetVisible(false);
        for (int i = 0; i < comparisonGrid.Count; i++) comparisonGrid[i].SetVisible(false);
        groupShiftedDown = false;
    }

    // ── Displacement track aid ────────────────────────────────────────────

    private void UpdateTrackAid()
    {
        bool gateOpen = aidEnabled
                        && (currentAidType == "distance" || currentAidType == "velocity");

        if (!gateOpen)
        {
            if (tracksActive) EndTracks();
            return;
        }

        bool grabbing = AnyGrabActive();

        if (grabbing && !tracksActive) BeginTracks();
        else if (!grabbing && tracksActive) EndTracks();

        if (tracksActive) UpdateTrackLengths();
    }

    /// <summary>
    /// Tracking is system-wide, not per-object. In 3-1 and 3-2 the student
    /// only ever grabs the rope - the movable pulley is driven, never held -
    /// so a per-object rule would leave its track blank and destroy the
    /// 2:1 comparison the question depends on.
    /// </summary>
    private void BeginTracks()
    {
        if (!ResolveTrackedTransforms()) return;

        if (trackFadeCoroutine != null)
        {
            StopCoroutine(trackFadeCoroutine);
            trackFadeCoroutine = null;
        }

        Vector3 side = PlaneSide();

        trackLoadStartY = trackedLoad.position.y;
        trackFreeStartY = trackedFree.position.y;

        trackLoad.Begin(trackedLoad.position, OutwardSide(trackedLoad) * trackLateralOffset);
        trackFree.Begin(trackedFree.position, OutwardSide(trackedFree) * trackLateralOffset);
        trackLoad.SetAlpha(1f);
        trackFree.SetAlpha(1f);

        tracksActive = true;
    }

    private bool ResolveTrackedTransforms()
    {
        Transform freeEnd = pulleyPhysics.GetFreeEndHook();
        if (freeEnd == null) freeEnd = hookR;
        if (freeEnd == null) return false;

        trackedFree = freeEnd;

        if (pulleyPhysics.IsMovablePulleyConfig())
        {
            trackedLoad = movablePulley;
        }
        else
        {
            Transform hL = pulleyPhysics.hookLeft != null ? pulleyPhysics.hookLeft : hookL;
            Transform hR = pulleyPhysics.hookRight != null ? pulleyPhysics.hookRight : hookR;
            trackedLoad = (freeEnd == hL) ? hR : hL;
        }

        return trackedLoad != null;
    }

    private void UpdateTrackLengths()
    {
        if (trackedLoad == null || trackedFree == null) return;
        trackLoad.UpdateLength(trackedLoad.position.y - trackLoadStartY);
        trackFree.UpdateLength(trackedFree.position.y - trackFreeStartY);
    }

    private void EndTracks()
    {
        tracksActive = false;
        if (trackFadeOutDelay <= 0.0001f)
        {
            EndTracksImmediate();
            return;
        }
        if (trackFadeCoroutine != null) StopCoroutine(trackFadeCoroutine);
        trackFadeCoroutine = StartCoroutine(FadeTracksOut());
    }

    private IEnumerator FadeTracksOut()
    {
        float t = 0f;
        while (t < trackFadeOutDelay)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / trackFadeOutDelay);
            trackLoad.SetAlpha(a);
            trackFree.SetAlpha(a);
            yield return null;
        }
        EndTracksImmediate();
        trackFadeCoroutine = null;
    }

    private void EndTracksImmediate()
    {
        tracksActive = false;
        if (trackLoad != null) trackLoad.Clear();
        if (trackFree != null) trackFree.Clear();
    }

    // ── Formula panel ─────────────────────────────────────────────────────

    private void UpdateFormulaPanel()
    {
        bool gateOpen = aidEnabled && currentAidType == "velocity";

        if (!gateOpen)
        {
            if (formulaShown)
            {
                formulaPanel.Hide();
                formulaShown = false;
                formulaShownAsMA2 = false;
            }
            return;
        }

        // Whether this question has an acceleration row is a property of the
        // QUESTION, not of the equipment currently standing on the table.
        // Reading it from the live mechanical advantage meant 3-2 showed 1-2's
        // panel until the movable pulley happened to be placed, then silently
        // swapped - the panel was reporting the rig instead of the task.
        bool ma2 = QuestionUsesMovablePulley();

        if (!formulaShown || ma2 != formulaShownAsMA2)
        {
            // Both velocity questions (1-2 and 3-2) open in formulaInitialState
            // (Collapsed by default) and are stepped through by the student:
            // Show formula -> More -> Hide.
            formulaPanel.Show(ma2, formulaInitialState);
            formulaPanel.SetAlpha(1f);
            formulaShown = true;
            formulaShownAsMA2 = ma2;
            if (verboseLogging)
                Debug.Log("[ConceptualAid] Formula panel shown, movablePulley=" + ma2);
        }

        Transform anchor = fixedPulley != null ? fixedPulley : movablePulley;
        if (anchor == null) return;

        Vector3 side = PlaneSide();
        Vector3 forward = towardViewer.sqrMagnitude < 0.0001f
            ? Vector3.zero : towardViewer.normalized;
        // Face the panel along towardViewer, not along arrowPlaneNormal: the
        // arrows are billboarded LineRenderers so their plane normal never
        // reveals a wrong facing, but text does - readable only when its local
        // +Z points at the reader.
        Vector3 faceDir = towardViewer.sqrMagnitude < 0.0001f
            ? Vector3.back : towardViewer.normalized;
        if (flipFormulaPanelFacing) faceDir = -faceDir;

        formulaPanel.SetSizes(formulaFontSize, formulaExplanationFontScale,
            formulaButtonFontScale, formulaMarkerFontSize, formulaCornerRadiusMeters);
        formulaPanel.SetOpensUpward(formulaOpensUpward);

        Vector3 panelPos = anchor.position
            + side * formulaPanelOffset.x
            + Vector3.up * formulaPanelOffset.y
            + forward * (aidForwardOffset + comparisonForwardOffset);
        formulaPanel.SetPosition(panelPos, faceDir, formulaPanelRotation);

        if (verboseLogging && Time.time >= nextPanelLogTime)
        {
            nextPanelLogTime = Time.time + 3f;
            Debug.Log("[ConceptualAid] Formula panel at " + panelPos
                      + " fontSize=" + formulaFontSize
                      + " | " + formulaPanel.DescribeState());
        }

        if (showFormulaMarkers && ResolveTrackedTransforms())
        {
            formulaPanel.SetMarkerPositions(
                trackedLoad.position + OutwardSide(trackedLoad) * (trackLateralOffset + 0.05f),
                trackedFree.position + OutwardSide(trackedFree) * (trackLateralOffset + 0.05f),
                faceDir, formulaPanelRotation);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Nudge an arrow toward the viewer and lift it clear of the table.
    /// If the tip would drop below the table surface the whole arrow is moved
    /// up bodily - the length is never trimmed, because a shortened arrow is
    /// no longer comparable to any other arrow.
    /// </summary>
    private Vector3 PlaceArrow(Vector3 start, Vector3 dir, float length)
    {
        Vector3 forward = towardViewer.sqrMagnitude < 0.0001f
            ? Vector3.zero : towardViewer.normalized;
        start += forward * aidForwardOffset;

        float tipY = start.y + dir.normalized.y * length;
        float lowest = Mathf.Min(start.y, tipY);
        if (lowest < minWorldY)
            start.y += (minWorldY - lowest);

        return start;
    }

    /// <summary>
    /// The side of an object that faces away from the rig centre, so aids sit
    /// on the outside of the apparatus instead of being buried between the
    /// object and the rope.
    /// </summary>
    /// <summary>
    /// Which side of the rig centre a comparison slot's group belongs on,
    /// as -1 or +1 along the in-plane side axis.
    /// </summary>
    private float SlotSign(Transform obj, Vector3 centre, Vector3 side)
    {
        if (obj == null) return -1f;
        return Vector3.Dot(obj.position - centre, side) >= 0f ? 1f : -1f;
    }

    private Vector3 OutwardSide(Transform obj)
    {
        Vector3 side = PlaneSide();
        Transform centre = fixedPulley != null ? fixedPulley : transform;
        if (obj == null) return side;
        float d = Vector3.Dot(obj.position - centre.position, side);
        return d >= 0f ? side : -side;
    }

    /// <summary>The "right" direction inside the experiment plane.</summary>
    private Vector3 PlaneSide()
    {
        Vector3 n = arrowPlaneNormal.sqrMagnitude < 0.0001f ? Vector3.back : arrowPlaneNormal.normalized;
        Vector3 s = Vector3.Cross(Vector3.up, n);
        if (s.sqrMagnitude < 0.0001f) s = Vector3.right;
        return s.normalized;
    }

    private Vector3 RopeDirection(Transform from, Transform toward)
    {
        if (forceArrowsAxisAligned) return Vector3.up;
        if (from == null) return Vector3.up;
        if (toward == null) return Vector3.up;
        Vector3 d = toward.position - from.position;
        if (d.sqrMagnitude < 0.000001f) return Vector3.up;
        return d.normalized;
    }

    /// <summary>
    /// Total mass of a weight chain, summed live from its Rigidbodies.
    /// Weights chain by parenting, so GetComponentsInChildren on the head of
    /// the chain reaches every weight hanging below it.
    /// </summary>
    private float ChainMass(GameObject chainHead)
    {
        if (chainHead == null) return 0f;
        float total = 0f;
        Rigidbody[] bodies = chainHead.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
            total += bodies[i].mass;
        return total;
    }

    /// <summary>
    /// Bottom of a weight chain. The W arrow starts at the bottom face of the
    /// lowest weight and its length is set by the force alone - it must never
    /// be stretched to span the whole stack of weights.
    /// </summary>
    private Vector3 ChainBottom(GameObject chainHead, Transform fallback)
    {
        if (chainHead == null)
            return fallback != null ? fallback.position : transform.position;

        Renderer[] renderers = chainHead.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return chainHead.transform.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Vector3 p = chainHead.transform.position;
        return new Vector3(p.x, b.min.y, p.z);
    }
}