using UnityEngine;
using System.Collections;

/// <summary>
/// Returns the movable pulley to where it stood before anyone touched it, with
/// a warning, when it is released somewhere it cannot attach to the rope.
///
/// This is a passive service: it does not watch the pulley itself.
/// MovablePulleyLock already detects an out-of-range release, and it calls
/// TryWarnAndReturnHome at exactly that moment. An earlier version polled on
/// its own timer, which ran alongside MovablePulleyLock's existing reset and the
/// two fought - the pulley jumped to the configured spot, then moved again.
///
/// At high fidelity only the pulley is returned; the weights are left where the
/// student put them. At low fidelity this declines, and MovablePulleyLock falls
/// back to resetting the whole configuration, since the system placed
/// everything there in the first place.
/// </summary>
public class MovablePulleyGuard : MonoBehaviour
{
    [Header("References")]
    public Transform movablePulley;
    public ResetManager resetManager;
    public WarningUI warningUI;
    public FidelityManager fidelityManager;

    [Header("Behaviour")]
    [Tooltip("Fidelity levels at which a misplaced pulley is returned on its own. " +
             "At other levels MovablePulleyLock resets the whole configuration.")]
    public int[] activeLevels = new int[] { 2, 3 };
    [Tooltip("Glide time back to the start position. A teleport gives the student " +
             "no chance to connect the warning with what moved.")]
    public float returnDuration = 0.3f;
    public string warningMessage = "The movable pulley must hang on the rope";

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool homeCaptured = false;
    private Coroutine returnCoroutine;

    // Home is captured on the first Update rather than in Start. ResetManager
    // records initial positions in its own Start, and Unity does not guarantee
    // the order different scripts' Start methods run in - asking from here could
    // reach it before the positions existed. By the first Update every Start in
    // the scene has finished, and nothing has been touched yet.
    void Update()
    {
        if (homeCaptured || movablePulley == null) return;

        homePosition = resetManager != null
            ? resetManager.GetInitialPosition(movablePulley.gameObject)
            : movablePulley.position;
        homeRotation = movablePulley.rotation;
        homeCaptured = true;
    }

    /// <summary>
    /// Called by MovablePulleyLock when the pulley is released out of snap range.
    /// Returns true if the guard took care of it; false means the caller should
    /// fall back to its own handling.
    /// </summary>
    public bool TryWarnAndReturnHome()
    {
        if (!homeCaptured || movablePulley == null) return false;
        if (!IsActiveAtCurrentLevel()) return false;

        // Already on its way home: still handled, do not restart the glide.
        if (returnCoroutine != null) return true;

        returnCoroutine = StartCoroutine(WarnAndReturn());
        return true;
    }

    private bool IsActiveAtCurrentLevel()
    {
        if (fidelityManager == null) return true;
        int level = fidelityManager.GetCurrentLevel();
        return System.Array.IndexOf(activeLevels, level) >= 0;
    }

    private IEnumerator WarnAndReturn()
    {
        if (warningUI != null) warningUI.ShowWarning(warningMessage);

        Rigidbody rb = movablePulley.GetComponent<Rigidbody>();
        bool wasKinematic = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true;   // keep physics from pulling it off course mid-glide

        Vector3 from = movablePulley.position;
        Quaternion fromRot = movablePulley.rotation;

        float t = 0f;
        while (t < returnDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / returnDuration);
            k = k * k * (3f - 2f * k);
            movablePulley.position = Vector3.Lerp(from, homePosition, k);
            movablePulley.rotation = Quaternion.Slerp(fromRot, homeRotation, k);
            yield return null;
        }

        movablePulley.position = homePosition;
        movablePulley.rotation = homeRotation;

        if (rb != null)
        {
            if (!wasKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = wasKinematic;
        }

        returnCoroutine = null;
    }
}