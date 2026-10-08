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
/// Owns the support state shown in the scene and the CL-based "shadow level".
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
/// Shadow level: the level the CL score maps to, computed in EVERY group with
/// the same thresholds and buffer. QuizManager uses it for the immediate-hard
/// rule, so the challenge escalation rule is identical across groups. Only in
/// group C does the shadow level also drive what the scene shows.
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
             "score no longer drives the scene. The shadow level keeps updating.")]
    public bool manualMode = false;

    [Header("Current State (Read Only)")]
    [SerializeField] private bool aidOn = false;
    [SerializeField] private bool lowFidelity = false;
    [SerializeField] private int shadowLevel = MaxLevel;

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
    private ExperimentGroup _lastGroup;

    // -- Lifecycle ----------------------------------------------------------

    private void Awake()
    {
        // Every group starts at Level 2: manual setup, no aid.
        aidOn = false;
        lowFidelity = false;
        shadowLevel = MaxLevel;
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

        if (!Mathf.Approximately(_lastCLScore, currentCLScore))
        {
            _lastCLScore = currentCLScore;
            UpdateShadowLevel();
        }

        // Flush a queued change as soon as both hands are free.
        if (hasPending && !AnyGrabActive())
        {
            hasPending = false;
            ApplySupport(pendingAid, pendingLow, pendingSource);
        }
    }

    // -- Shadow level (all groups) -------------------------------------------

    private void UpdateShadowLevel()
    {
        if (config == null) return;

        // Iterate until stable so a large CL jump lands on the correct level.
        int target = shadowLevel;
        for (int guard = 0; guard < 4; guard++)
        {
            int next = CalculateLevel(currentCLScore, target);
            if (next == target) break;
            target = next;
        }

        if (target != shadowLevel)
        {
            int old = shadowLevel;
            shadowLevel = target;
            SessionLogger.Log("ShadowLevelChanged", "CL",
                              SessionLogger.LevelName(old) + " -> " + SessionLogger.LevelName(target));
        }

        // Only group C lets the CL score drive the scene.
        if (group == ExperimentGroup.C_Adaptive && !manualMode)
            RequestLevel(shadowLevel, "CL");
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
        _lastCLScore = -1f; // force a re-evaluation on the next frame
    }

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
    public int GetShadowLevel() { return shadowLevel; }

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
