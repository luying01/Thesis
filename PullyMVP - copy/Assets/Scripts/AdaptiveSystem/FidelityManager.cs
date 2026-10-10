using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The three experiment groups. Chosen once per participant, before Play.
/// </summary>
public enum ExperimentGroup
{
    A_NoSupport,       // fixed at Level 2 for the whole session
    B_LearnerControl,  // participant asks out loud, experimenter switches aid / auto setup
    C_Adaptive         // CL classifier drives the level
}

/// <summary>
/// Owns the support state shown in the scene and the CL-based "fidelity level".
///
/// Two dimensions of support:
///   aid          conceptual aid (force / velocity / distance cues) on or off
///   lowFidelity  system places the equipment (Play button) instead of the student
///
/// The three-level ladder used by group C is a path through those two:
///   Level 2 = manual setup, no aid      (low CL)
///   Level 1 = manual setup + aid
///   Level 0 = auto setup  + aid         (high CL)
/// Group B can also reach the fourth combination, auto setup without aid
/// (reported as AutoNoAidCode).
///
/// fidelity level: the level the CL score maps to, computed in EVERY group with
/// the same thresholds and buffer. QuizManager uses it for the immediate-hard
/// rule, so the challenge escalation rule is identical across groups. Only in
/// group C does the fidelity level also drive what the scene shows.
///
/// Kept from the previous version:
///   - multi-step evaluation, so a large CL jump lands on the right level;
///   - changes requested while the participant holds something are queued and
///     applied on release (all groups);
///   - manual override for group C (ForceLevel / ReturnToCLControl).
/// </summary>
public class FidelityManager : MonoBehaviour
{
    public const int MaxLevel = 2;
    public const int MinLevel = 0;
    /// <summary>Display code for "auto setup, no aid". Only reachable in group B.</summary>
    public const int AutoNoAidCode = 3;

    [Header("Experiment Group (set before Play)")]
    public ExperimentGroup group = ExperimentGroup.C_Adaptive;

    [Header("Configuration")]
    public FidelityConfig config;

    [Header("CL Input (0-100)")]
    [Tooltip("Fed by CognitiveLoadController in every group.")]
    [Range(0f, 100f)]
    public float currentCLScore = 0f;

    [Header("Manual Override (group C fallback)")]
    [Tooltip("Group C only: on = the level is set by the Wizard panel and the CL " +
             "score no longer drives the scene. The fidelity level keeps updating.")]
    public bool manualMode = false;

    [Header("Current State (Read Only)")]
    [Tooltip("The level the scene is showing right now.")]
    [SerializeField] private string displayedLevel = "L2";
    [Tooltip("The level the current CL corresponds to, in real time. Moves up " +
             "and down with CL in every group and is never reset or held. In " +
             "group C the scene follows it upwards only.")]
    [SerializeField] private string actualLevel = "L2";
    [SerializeField] private bool aidOn = false;
    [SerializeField] private bool lowFidelity = false;

    // Level the current CL corresponds to (see actualLevel).
    private int fidelityLevel = MaxLevel;

    [Header("Events")]
    [Tooltip("Invoked whenever aid or setup mode changes. The int is the display " +
             "code (2/1/0, or 3 = auto setup without aid). Listeners should read " +
             "IsAidOn() / IsLowFidelity() rather than decode the int.")]
    public UnityEvent<int> onFidelityLevelChanged;

    [Header("Deferred Switching")]
    [Tooltip("Queue changes that arrive while the participant is holding " +
             "something, and apply them once they let go.")]
    public bool deferWhileGrabbing = true;
    public RopeGrab ropeGrabLeft;
    public RopeGrab ropeGrabRight;
    public PulleyPhysics pulleyPhysics;

    [Header("Debug (Read Only)")]
    [SerializeField] private bool hasPending = false;
    [SerializeField] private bool pendingAid = false;
    [SerializeField] private bool pendingLow = false;
    private string pendingSource = "";

    private float _lastCLScore = -1f;

    // True during the pause after a correct answer: CL changes are ignored.
    private bool frozen = false;

    [Header("Question Start Hold")]
    [Tooltip("Seconds at the start of every question during which the level is " +
             "held at Level 2 and CL is ignored. When the hold ends, the current " +
             "CL is evaluated straight away.")]
    public float questionStartHoldSeconds = 2f;
    private float holdUntil = -1f;

    // Group C: the level the scene is allowed to be at within the current
    // question. Only moves towards more support; back to Level 2 per question.
    private int sceneLevel = MaxLevel;
    private ExperimentGroup _lastGroup;

    // -- Lifecycle ----------------------------------------------------------

    private void Awake()
    {
        // Every group starts at Level 2: manual setup, no aid.
        aidOn = false;
        lowFidelity = false;
        fidelityLevel = MaxLevel;
        hasPending = false;
        manualMode = false;
        _lastGroup = group;
    }

    private void Start()
    {
        SessionLogger.Log("GroupSet", "Setup", "group=" + GetGroupLabel());
    }

    private void Update()
    {
        // Group is meant to be fixed before Play; handle a change anyway.
        if (group != _lastGroup)
        {
            _lastGroup = group;
            OnGroupChanged();
        }

        // The fidelity level follows every new CL value, always.
        if (!Mathf.Approximately(_lastCLScore, currentCLScore))
        {
            _lastCLScore = currentCLScore;
            UpdateFidelityLevel();
            UpdateSceneLevel();
        }

        // End of the question-start hold: let the scene catch up right away.
        if (holdUntil > 0f && Time.time >= holdUntil)
        {
            holdUntil = -1f;
            UpdateSceneLevel();
        }

        // Flush a queued change as soon as both hands are free.
        if (hasPending && !AnyGrabActive())
        {
            hasPending = false;
            ApplySupport(pendingAid, pendingLow, pendingSource);
        }

        // Inspector read-outs.
        displayedLevel = DescribeLevel(GetCurrentLevel());
        actualLevel = DescribeLevel(fidelityLevel) + "   (CL " + currentCLScore.ToString("F0") + ")";
    }

    /// <summary>Readable name of a level, for the Inspector and the Wizard panel.</summary>
    public static string DescribeLevel(int code)
    {
        switch (code)
        {
            case 2: return "L2  manual setup, no aid";
            case 1: return "L1  manual setup + aid";
            case 0: return "L0  auto setup + aid";
            default: return "Auto  auto setup, no aid";
        }
    }

    // -- fidelity level (all groups): the real-time level -------------------------

    /// <summary>
    /// The level the current CL maps to, updated live in both directions.
    /// Not affected by the one-way rule, the hold, the pause or question
    /// changes. QuizManager reads it at Confirm for the immediate-hard rule.
    /// </summary>
    private void UpdateFidelityLevel()
    {
        if (config == null) return;

        // Iterate until stable so a large CL jump lands on the correct level.
        int target = fidelityLevel;
        for (int guard = 0; guard < 4; guard++)
        {
            int next = CalculateLevel(currentCLScore, target);
            if (next == target) break;
            target = next;
        }

        if (target != fidelityLevel)
        {
            int old = fidelityLevel;
            fidelityLevel = target;
            SessionLogger.Log("FidelityLevelChanged", "CL",
                              SessionLogger.LevelName(old) + " -> " + SessionLogger.LevelName(target));
        }
    }

    // -- Scene level (group C only) ----------------------------------------------

    /// <summary>
    /// Group C's scene follows the fidelity level, with three restrictions:
    /// within a question support is only added, never removed; nothing changes
    /// during the hold at the start of a question or the pause after a correct
    /// answer; and each new question starts again at Level 2.
    /// </summary>
    private void UpdateSceneLevel()
    {
        if (group != ExperimentGroup.C_Adaptive || manualMode) return;
        if (frozen || holdUntil > 0f) return;

        if (fidelityLevel < sceneLevel)
        {
            sceneLevel = fidelityLevel;
            RequestLevel(sceneLevel, "CL");
        }
    }

    private int CalculateLevel(float cl, int current)
    {
        float b = config.buffer;

        // CL rising: switch to lower fidelity
        if (current == 2 && cl >= config.boundary2to1 + b) return 1;
        if (current == 1 && cl >= config.boundary1to0 + b) return 0;

        // CL falling: switch to higher fidelity
        if (current == 0 && cl < config.boundary1to0 - b) return 1;
        if (current == 1 && cl < config.boundary2to1 - b) return 2;

        return current;
    }

    // -- Support state ------------------------------------------------------

    private static void LevelToSupport(int level, out bool aid, out bool low)
    {
        level = Mathf.Clamp(level, MinLevel, MaxLevel);
        aid = level <= 1;   // Level 1 and 0
        low = level == 0;   // Level 0 only
    }

    private void RequestLevel(int level, string source)
    {
        bool aid, low;
        LevelToSupport(level, out aid, out low);
        RequestSupport(aid, low, source);
    }

    private void RequestSupport(bool aid, bool low, string source)
    {
        if (aid == aidOn && low == lowFidelity)
        {
            // Already there; a queued change is no longer needed.
            hasPending = false;
            return;
        }

        if (deferWhileGrabbing && AnyGrabActive())
        {
            bool changed = !hasPending || pendingAid != aid || pendingLow != low;
            hasPending = true;
            pendingAid = aid;
            pendingLow = low;
            pendingSource = source;
            if (changed)
                SessionLogger.Log("SupportChangeDeferred", source,
                                  "to " + SessionLogger.LevelName(CodeOf(aid, low)) +
                                  " (waiting for release)");
            return;
        }

        ApplySupport(aid, low, source);
    }

    private void ApplySupport(bool aid, bool low, string source)
    {
        if (aid == aidOn && low == lowFidelity) return;

        int from = GetCurrentLevel();
        aidOn = aid;
        lowFidelity = low;
        int to = GetCurrentLevel();

        onFidelityLevelChanged.Invoke(to);
        SessionLogger.Log("SupportChanged", source,
                          SessionLogger.LevelName(from) + " -> " + SessionLogger.LevelName(to));
        Debug.Log("[FidelityManager] Support " + from + " -> " + to +
                  " (aid=" + aidOn + ", auto=" + lowFidelity + ", source=" + source +
                  ", CL=" + currentCLScore.ToString("F0") + ")");
    }

    private void OnGroupChanged()
    {
        manualMode = false;
        hasPending = false;
        ApplySupport(false, false, "GroupReset");
        sceneLevel = MaxLevel;
        _lastCLScore = -1f; // re-evaluate on the next frame
        SessionLogger.Log("GroupSet", "Setup", "group=" + GetGroupLabel());
    }

    private bool AnyGrabActive()
    {
        if (ropeGrabLeft != null && ropeGrabLeft.isGrabbed) return true;
        if (ropeGrabRight != null && ropeGrabRight.isGrabbed) return true;
        if (pulleyPhysics != null && pulleyPhysics.IsLoadGrabbed()) return true;
        return false;
    }

    private static int CodeOf(bool aid, bool low)
    {
        if (!low) return aid ? 1 : 2;
        return aid ? 0 : AutoNoAidCode;
    }

    // -- Experimenter controls ------------------------------------------------

    /// <summary>
    /// Set a ladder level directly.
    /// Group B: a shortcut for a learner request ("Level 1, please").
    /// Group C: manual override; the CL score stops driving the scene.
    /// Group A: ignored.
    /// </summary>
    public void ForceLevel(int level)
    {
        if (group == ExperimentGroup.A_NoSupport)
        {
            Debug.LogWarning("[FidelityManager] Group A is fixed at Level 2; ForceLevel ignored.");
            return;
        }

        string source;
        if (group == ExperimentGroup.C_Adaptive)
        {
            manualMode = true;
            source = "WizardOverride";
        }
        else
        {
            source = "LearnerRequest";
        }

        RequestLevel(level, source);
    }

    /// <summary>Group B only: turn the conceptual aid on or off (learner request).</summary>
    public void SetAid(bool on)
    {
        if (group != ExperimentGroup.B_LearnerControl)
        {
            Debug.LogWarning("[FidelityManager] SetAid is for group B only.");
            return;
        }
        bool low = hasPending ? pendingLow : lowFidelity;
        RequestSupport(on, low, "LearnerRequest");
    }

    /// <summary>Group B only: turn automatic equipment setup on or off (learner request).</summary>
    public void SetAutoSetup(bool on)
    {
        if (group != ExperimentGroup.B_LearnerControl)
        {
            Debug.LogWarning("[FidelityManager] SetAutoSetup is for group B only.");
            return;
        }
        bool aid = hasPending ? pendingAid : aidOn;
        RequestSupport(aid, on, "LearnerRequest");
    }

    /// <summary>Group C: leave manual override; the CL score drives the scene again.</summary>
    public void ReturnToCLControl()
    {
        if (!manualMode) return;
        manualMode = false;
        SessionLogger.Log("ReturnToCLControl", "WizardOverride", "");
        // The scene goes to the level CL currently calls for, and continues
        // one-way from there.
        sceneLevel = fidelityLevel;
        RequestLevel(sceneLevel, "CL");
    }

    // -- Question boundaries (called by QuizManager) ---------------------------

    /// <summary>
    /// Every time a question appears (including the switch to its hard
    /// version): the scene returns to Level 2 in groups B and C (group B's hints and automatic
    /// setup are switched off; the participant can ask again). Group A is always
    /// Level 2. Group C's scene then ignores CL for questionStartHoldSeconds.
    /// The fidelity level is not reset: it always reflects the current CL.
    /// </summary>
    public void OnNewQuestion()
    {
        frozen = false;
        hasPending = false;
        sceneLevel = MaxLevel;

        bool resetScene = group == ExperimentGroup.B_LearnerControl
                       || (group == ExperimentGroup.C_Adaptive && !manualMode);
        if (resetScene)
            ApplySupport(false, false, "QuestionReset");

        // Hold the scene at Level 2 for the first seconds of the question.
        // (The fidelity level keeps following CL throughout.)
        holdUntil = questionStartHoldSeconds > 0f ? Time.time + questionStartHoldSeconds : -1f;
    }

    /// <summary>
    /// Freeze or unfreeze adaptation. QuizManager freezes it for the pause
    /// after a correct answer, so CL changes in that pause have no effect.
    /// </summary>
    public void SetFrozen(bool value)
    {
        frozen = value;
    }

    public bool IsFrozen() { return frozen; }

    // Called by CognitiveLoadController.
    public void SetCLScore(float score)
    {
        currentCLScore = Mathf.Clamp(score, 0f, 100f);
    }

    // -- Queries --------------------------------------------------------------

    /// <summary>
    /// Display code of what the scene shows: 2 / 1 / 0 on the ladder, or
    /// AutoNoAidCode (3) for auto setup without aid (group B only).
    /// </summary>
    public int GetCurrentLevel() { return CodeOf(aidOn, lowFidelity); }

    /// <summary>The level the CL score maps to, in every group.</summary>
    public int GetFidelityLevel() { return fidelityLevel; }

    public bool IsAidOn() { return aidOn; }

    /// <summary>True when the system places the equipment (Play button).</summary>
    public bool IsLowFidelity() { return lowFidelity; }

    /// <summary>True while a change is waiting for the participant to let go.</summary>
    public bool HasPendingLevelChange() { return hasPending; }

    public bool GetPendingAid() { return pendingAid; }
    public bool GetPendingAutoSetup() { return pendingLow; }

    public string GetGroupLabel()
    {
        switch (group)
        {
            case ExperimentGroup.A_NoSupport: return "A";
            case ExperimentGroup.B_LearnerControl: return "B";
            default: return "C";
        }
    }
}
