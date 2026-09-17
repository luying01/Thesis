using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Maps a cognitive-load score onto a fidelity level (0-3) with hysteresis.
///
/// Two behaviours were added on top of the original version:
///
/// 1. Multi-step evaluation. CalculateLevel only moves one step at a time, and
///    Update only re-evaluates when the CL score changes. A single large jump
///    (e.g. 0 -> 95, then held constant) therefore used to stop one step in
///    and never arrive. EvaluateLevel now iterates until the level is stable.
///    This matters most for the Wizard-of-Oz setup, where the score is set in
///    one move rather than drifting continuously.
///
/// 2. Deferred switching while grabbing. A fidelity change in the middle of a
///    manipulation is both more noticeable and more disruptive, so a change
///    requested while the participant is holding something is queued and
///    applied on release.
/// </summary>
public class FidelityManager : MonoBehaviour
{
    [Header("Configuration")]
    public FidelityConfig config;

    [Header("CL Input (0-100)")]
    [Range(0f, 100f)]
    public float currentCLScore = 0f;

    [Header("Current Level (Read Only)")]
    [SerializeField] private int currentLevel = 3;

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

    private void Update()
    {
        if (!Mathf.Approximately(_lastCLScore, currentCLScore))
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
        for (int guard = 0; guard < 8; guard++)
        {
            int next = CalculateLevel(currentCLScore, target);
            if (next == target) break;
            target = next;
        }

        if (target == currentLevel)
        {
            // A queued change that is no longer needed should be dropped.
            if (pendingLevel == currentLevel) pendingLevel = -1;
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
        float h = config.hysteresis;

        // CL rising: switch to lower fidelity
        if (current == 3 && cl >= config.toLevel2) return 2;
        if (current == 2 && cl >= config.toLevel1) return 1;
        if (current == 1 && cl >= config.toLevel0) return 0;

        // CL falling: switch to higher fidelity
        if (current == 0 && cl < config.toLevel0 - h) return 1;
        if (current == 1 && cl < config.toLevel1 - h) return 2;
        if (current == 2 && cl < config.toLevel2 - h) return 3;

        return current;
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
