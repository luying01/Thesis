using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// The scene's single warning surface. Every warning - rope angle, connecting
/// rope angle, misplaced movable pulley - goes through ShowWarning so they all
/// look and behave the same; two differently styled warnings read as two
/// different kinds of information.
///
/// Layout is icon-then-text, left aligned, on a panel that sizes itself to the
/// message. Appearance comes from the shared PanelStyle asset.
/// </summary>
public class WarningUI : MonoBehaviour
{
    [Header("UI Reference")]
    public CanvasGroup canvasGroup;
    [Tooltip("The rounded backdrop. Needs an Image, a HorizontalLayoutGroup, " +
             "a ContentSizeFitter and a PanelStyleApplier.")]
    public RectTransform panel;
    public TextMeshProUGUI warningIcon;
    public TextMeshProUGUI warningText;
    public PanelStyleApplier styleApplier;

    [Header("Style")]
    public PanelStyle style;
    [Tooltip("Glyph shown to the left of the message. If it renders as a box, " +
             "the TMP font has no U+26A0 - fall back to \"!\".")]
    public string iconGlyph = "\u26A0";
    public float iconFontSize = 28f;
    public float textFontSize = 24f;
    [Tooltip("Longest the message may run before it wraps, in canvas units. " +
             "Without a cap a long warning stretches the panel off screen.")]
    public float maxTextWidth = 420f;
    [Tooltip("Gap between the icon and the text.")]
    public float spacing = 12f;

    [Header("Padding (canvas units)")]
    [Tooltip("Space between the panel edge and its contents. Kept on the " +
             "warning itself rather than read from PanelStyle: the style's " +
             "padding is in metres for the world-space formula panel, and " +
             "sharing it meant tuning one panel quietly re-laid out the other.")]
    public int paddingLeft = 24;
    public int paddingRight = 28;
    public int paddingTop = 16;
    public int paddingBottom = 16;

    [Header("Settings")]
    [Tooltip("Shortest time a warning stays up, however brief the message.")]
    public float displayDuration = 2f;
    public float fadeDuration = 0.5f;
    [Tooltip("Extra time per character, so longer messages stay up long enough to " +
             "read. Reading in a headset is slower than on a screen, and the " +
             "student usually has to shift attention from the apparatus first.")]
    public float secondsPerCharacter = 0.08f;
    [Tooltip("Time to notice the warning before reading starts.")]
    public float readLeadTime = 1f;
    [Tooltip("Upper bound, so a very long message cannot linger indefinitely.")]
    public float maxDisplayDuration = 8f;

    [Header("Free End Angle Detection")]
    public PulleyPhysics pulleyPhysics;
    public float freeEndWarningAngle = 30f;
    public string freeEndWarningMessage = "Keep the rope vertical";

    [Header("Connecting Rope Angle Detection")]
    public PulleySystem pulleySystem;
    public MovablePulleyLock movablePulleyLock;
    public float connectingRopeWarningAngle = 20f;
    public string connectingRopeWarningMessage = "Try to keep the connecting rope vertical";
    public FidelityManager fidelityManager;

    private Coroutine fadeCoroutine;
    private bool isShowing = false;

    void Start()
    {
        ApplyStyle();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (warningText != null) warningText.text = "";
        if (panel != null) panel.gameObject.SetActive(false);
    }

    // Runs whenever a field on this component is edited - in Play mode too -
    // so padding and font sizes can be tuned live while a warning is showing.
    void OnValidate()
    {
        ApplyStyle();
    }

    /// <summary>
    /// Push font sizes, spacing and padding into the layout. Kept in code so
    /// one PanelStyle drives every panel rather than each being configured by
    /// hand in the Inspector and drifting apart.
    /// </summary>
    private void ApplyStyle()
    {
        if (warningIcon != null)
        {
            warningIcon.text = iconGlyph;
            warningIcon.fontSize = iconFontSize;
            warningIcon.alignment = TextAlignmentOptions.Midline;
        }

        if (warningText != null)
        {
            warningText.fontSize = textFontSize;
            warningText.alignment = TextAlignmentOptions.MidlineLeft;
            warningText.enableAutoSizing = false;

            LayoutElement le = warningText.GetComponent<LayoutElement>();
            if (le != null) le.preferredWidth = maxTextWidth;
        }

        if (panel != null)
        {
            HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = spacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.padding = new RectOffset(paddingLeft, paddingRight,
                                                paddingTop, paddingBottom);
                LayoutRebuilder.MarkLayoutForRebuild(panel);
            }
        }

        if (styleApplier != null)
        {
            styleApplier.style = style;
            styleApplier.Apply();
        }
    }

    void Update()
    {
        if (pulleyPhysics == null) return;

        CheckFreeEndAngle();

        if (pulleyPhysics.IsMovablePulleyConfig())
            CheckConnectingRopeAngle();
    }

    // ©¤©¤ Free End Angle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void CheckFreeEndAngle()
    {
        RopeGrab ropeGrabLeft = pulleyPhysics.hookLeft != null
            ? pulleyPhysics.hookLeft.GetComponent<RopeGrab>() : null;
        RopeGrab ropeGrabRight = pulleyPhysics.hookRight != null
            ? pulleyPhysics.hookRight.GetComponent<RopeGrab>() : null;

        // Check whichever end is actually in the student's hand, measured
        // against the slot that end hangs from.
        //
        // freeEndHook is only ever assigned by PulleySystem.NotifyPulleyPhysics,
        // which runs in the movable-pulley path. On a single fixed pulley it
        // therefore keeps whatever it held at startup, so one side was checked
        // forever and the other never at all.
        Transform freeEnd = null;
        Transform slotRef = null;

        if (ropeGrabLeft != null && ropeGrabLeft.isGrabbed)
        {
            freeEnd = pulleyPhysics.hookLeft;
            slotRef = pulleyPhysics.slotLeft;
        }
        else if (ropeGrabRight != null && ropeGrabRight.isGrabbed)
        {
            freeEnd = pulleyPhysics.hookRight;
            slotRef = pulleyPhysics.slotRight;
        }

        if (freeEnd == null || slotRef == null) return;

        Vector3 ropeDir = freeEnd.position - slotRef.position;
        float angle = Vector3.Angle(Vector3.down, ropeDir);

        if (angle > freeEndWarningAngle && !isShowing)
            ShowWarning(freeEndWarningMessage);
    }

    // ©¤©¤ Connecting Rope Angle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void CheckConnectingRopeAngle()
    {
        // Only meaningful while the student is assembling the rig by hand. At
        // low fidelity the pulley may sit locked with no weight attached
        // between automated steps, which is not an error.
        if (fidelityManager != null)
        {
            int level = fidelityManager.GetCurrentLevel();
            bool isHighFidelity = (level == 2 || level == 3);
            if (!isHighFidelity) return;
        }

        if (pulleySystem == null) return;
        if (pulleySystem.cachedFixedSlot == null) return;
        if (movablePulleyLock == null || !movablePulleyLock.isLocked) return;

        if (pulleyPhysics.weightChainLoad != null) return;
        if (pulleyPhysics.weightChainForce != null) return;
        if (pulleyPhysics.weightChainLeft != null) return;
        if (pulleyPhysics.weightChainRight != null) return;

        var lockedField = typeof(PulleySystem).GetField("lockedMovablePulley",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        Transform lockedMovable = null;
        if (lockedField != null)
            lockedMovable = lockedField.GetValue(pulleySystem) as Transform;

        if (lockedMovable == null) return;

        Transform movableSlotR = FindChildByName(lockedMovable, "RopeSlot_R");
        Transform movableSlotL = FindChildByName(lockedMovable, "RopeSlot_L");
        Transform fixedSlotL = FindChildByName(
            pulleySystem.cachedFixedSlot.snappedPulleyObject.transform, "RopeSlot_L");
        Transform fixedSlotR = FindChildByName(
            pulleySystem.cachedFixedSlot.snappedPulleyObject.transform, "RopeSlot_R");

        if (movableSlotR == null || movableSlotL == null ||
            fixedSlotL == null || fixedSlotR == null) return;

        Vector3 ropeDir;
        if (pulleySystem.movableIsRight)
            ropeDir = fixedSlotR.position - movableSlotL.position;
        else
            ropeDir = fixedSlotL.position - movableSlotR.position;

        float angle = Vector3.Angle(Vector3.up, ropeDir);

        if (angle > connectingRopeWarningAngle && !isShowing)
            ShowWarning(connectingRopeWarningMessage);
    }

    // ©¤©¤ Show Warning ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// The single entry point. MovablePulleyGuard and anything else that needs
    /// to tell the student something calls this.
    /// </summary>
    public void ShowWarning(string message)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(ShowAndFade(message));
    }

    private IEnumerator ShowAndFade(string message)
    {
        isShowing = true;

        if (panel != null) panel.gameObject.SetActive(true);
        if (warningText != null) warningText.text = message;
        if (warningIcon != null) warningIcon.text = iconGlyph;

        // Resolve the layout before the panel is visible, so it never appears
        // at the previous message's width and then snaps to the new one.
        if (panel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        if (styleApplier != null)
            styleApplier.Apply();

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // Scale the dwell with the message. One fixed duration is either too
        // short for the long warnings or needlessly long for the short ones.
        int chars = string.IsNullOrEmpty(message) ? 0 : message.Length;
        float dwell = Mathf.Clamp(readLeadTime + chars * secondsPerCharacter,
                                  displayDuration, maxDisplayDuration);

        yield return new WaitForSeconds(dwell);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - (elapsed / fadeDuration);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (panel != null) panel.gameObject.SetActive(false);
        isShowing = false;
    }

    // ©¤©¤ Helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private Transform FindChildByName(Transform parent, string partialName)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>())
            if (t.name.Contains(partialName)) return t;
        return null;
    }
}