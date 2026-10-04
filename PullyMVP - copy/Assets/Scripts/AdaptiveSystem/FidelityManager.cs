using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Maps a cognitive-load score onto a fidelity level (0-2) with a buffer.
///
///   Level 2 = high interaction fidelity, conceptual aid OFF   (low CL)
///   Level 1 = high interaction fidelity, conceptual aid ON
///   Level 0 = low interaction fidelity, conceptual aid ON     (high CL)
///
/// Each step changes exactly one dimension: 2->1 adds the aid, 1->0 lowers
/// interaction fidelity while the aid stays on.
///
/// Behaviours kept from the previous version:
///
/// 1. Multi-step evaluation. CalculateLevel only moves one step at a time, so
///    EvaluateLevel iterates until the level is stable. A single large jump
///    (e.g. 0 -> 95, as in the Wizard-of-Oz setup) lands on the right level.
///
/// 2. Deferred switching while grabbing. A change requested while the
///    participant is holding something is queued and applied on release.
///
/// 3. Manual mode (Wizard-of-Oz). ForceLevel sets the level directly and
///    switches the CL input off, so a CL value left in the slider cannot pull
///    the level back. ReturnToCLControl hands control back to the CL score.
/// </summary>
public class FidelityManager : MonoBehaviour
{
    public const int MaxLevel = 2;
    public const int MinLevel = 0;

    [Header("Configuration")]
    public FidelityConfig config;

    [Header("CL Input (0-100)")]
    [Range(0f, 100f)]
    public float currentCLScore = 0f;

    [Header("Start Level")]
    [Tooltip("Level the session starts at. Forced into 0-2 on Awake, so an old " +
             "scene value from the four-level version (3) cannot survive.")]
    [Range(0, 2)]
    public int startLevel = MaxLevel;

    [Header("Manual Mode (Wizard-of-Oz)")]
    [Tooltip("On: the level is set directly by ForceLevel and the CL score is " +
             "ignored. Turned on automatically by the Wizard panel buttons.")]
    public bool manualMode = false;

    [Header("Current Level (Read Only)")]
    [SerializeField] private int currentLevel = MaxLevel;

    [Header("Events")]
    public UnityEvent<int> onFidelityLevelChanged;

    [Header("Deferred Switching")]
    [Tooltip("Queue level changes that arrive while the participant is holding " +
             "something, and apply them once they let go.")]
    public bool deferWhileGrabbing = true;
    public RopeGrab ropeGrabLeft;
    public RopeGrab ropeGrabRight;
    public PulleyPhysics pulleyPhysics;

    [Header("Debug (Read Only)")]
    [SerializeField] private int pendingLevel = -1;

    private float _lastCLScore = -1f;

    private void Awake()
    {
        // The scene still stores currentLevel = 3 from the four-level version.
        // Override it here so every other script sees a valid level from frame 1.
        currentLevel = Mathf.Clamp(startLevel, MinLevel, MaxLevel);
        pendingLevel = -1;
    }

    private void Update()
    {
        if (!manualMode && !Mathf.Approximately(_lastCLScore, currentCLScore))
        {
            _lastCLScore = currentCLScore;
            EvaluateLevel();
        }

        // Flush a queued change as soon as both hands are free.
        if (pendingLevel >= 0 && !AnyGrabActive())
        {
            int level = pendingLevel;
            pendingLevel = -1;
            ApplyLevel(level);
        }
    }

    private void EvaluateLevel()
    {
        if (config == null) return;

        // Iterate until stable so a large CL jump lands on the correct level
        // instead of stopping after a single step.
        int target = currentLevel;
        for (int guard = 0; guard < 4; guard++)
        {
            int next = CalculateLevel(currentCLScore, target);
            if (next == target) break;
            target = next;
        }

        if (target == currentLevel)
        {
            // A queued change that is no longer needed should be dropped.
            pendingLevel = -1;
            return;
        }

        if (deferWhileGrabbing && AnyGrabActive())
        {
            pendingLevel = target;
            Debug.Log("[FidelityManager] Level change to " + target +
                      " deferred (participant is holding something)");
            return;
        }

        ApplyLevel(target);
    }

    private void ApplyLevel(int newLevel)
    {
        newLevel = Mathf.Clamp(newLevel, MinLevel, MaxLevel);
        if (newLevel == currentLevel) return;
        currentLevel = newLevel;
        onFidelityLevelChanged.Invoke(currentLevel);
        Debug.Log("[FidelityManager] Level changed to: " + currentLevel +
                  " (CL=" + currentCLScore + ")");
    }

    private bool AnyGrabActive()
    {
        if (ropeGrabLeft != null && ropeGrabLeft.isGrabbed) return true;
        if (ropeGrabRight != null && ropeGrabRight.isGrabbed) return true;
        if (pulleyPhysics != null && pulleyPhysics.IsLoadGrabbed()) return true;
        return false;
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

    /// <summary>
    /// Wizard-of-Oz: set the level directly, ignoring the CL score from now on.
    /// Still deferred while the participant is holding something.
    /// </summary>
    public void ForceLevel(int level)
    {
        manualMode = true;
        level = Mathf.Clamp(level, MinLevel, MaxLevel);

        if (level == currentLevel)
        {
            pendingLevel = -1;
            return;
        }

        if (deferWhileGrabbing && AnyGrabActive())
        {
            pendingLevel = level;
            Debug.Log("[FidelityManager] Manual level " + level +
                      " deferred (participant is holding something)");
            return;
        }

        ApplyLevel(level);
    }

    /// <summary>Leave manual mode; the CL score drives the level again.</summary>
    public void ReturnToCLControl()
    {
        manualMode = false;
        _lastCLScore = -1f; // force a re-evaluation on the next frame
    }

    // Teammate calls this to set CL score from their module
    public void SetCLScore(float score)
    {
        currentCLScore = Mathf.Clamp(score, 0f, 100f);
    }

    public int GetCurrentLevel() { return currentLevel; }

    /// <summary>True while a level change is waiting for the participant to let go.</summary>
    public bool HasPendingLevelChange() { return pendingLevel >= 0; }
}