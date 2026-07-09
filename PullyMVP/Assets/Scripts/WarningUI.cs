using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class WarningUI : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI warningText;
    public CanvasGroup canvasGroup;
    public Image background;                // Semi-transparent background panel

    [Header("Billboard")]
    public Camera mainCamera;
    public Transform fixedPulley;

    [Header("Settings")]
    public float displayDuration = 2f;
    public float fadeDuration = 0.5f;
    public float backgroundAlpha = 0.7f;    // Background transparency

    [Header("Free End Angle Detection")]
    public PulleyPhysics pulleyPhysics;
    public float freeEndWarningAngle = 30f;
    public string freeEndWarningMessage = "Keep the rope vertical";

    [Header("Connecting Rope Angle Detection")]
    public PulleySystem pulleySystem;
    public float connectingRopeWarningAngle = 20f;
    public string connectingRopeWarningMessage = "Try to keep the connecting rope vertical";

    private Coroutine fadeCoroutine;
    private bool isShowing = false;

    void Start()
    {
        canvasGroup.alpha = 0f;
        warningText.text = "";
        warningText.alpha = 0f;
        SetBackgroundAlpha(0f);

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        if (pulleyPhysics == null) return;

        if (pulleyPhysics.IsMovablePulleyConfig())
        {
            CheckFreeEndAngle();
            CheckConnectingRopeAngle();
        }
    }

    // ©¤©¤ Free End Angle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void CheckFreeEndAngle()
    {
        Transform freeEnd = pulleyPhysics.freeEndHook;
        Transform slotRef = pulleyPhysics.slotRight;

        if (freeEnd == null || slotRef == null) return;

        RopeGrab ropeGrabLeft = pulleyPhysics.hookLeft != null
            ? pulleyPhysics.hookLeft.GetComponent<RopeGrab>() : null;
        RopeGrab ropeGrabRight = pulleyPhysics.hookRight != null
            ? pulleyPhysics.hookRight.GetComponent<RopeGrab>() : null;

        bool isGrabbing = (ropeGrabLeft != null && ropeGrabLeft.isGrabbed) ||
                          (ropeGrabRight != null && ropeGrabRight.isGrabbed);

        if (!isGrabbing) return;

        Vector3 ropeDir = freeEnd.position - slotRef.position;
        float angle = Vector3.Angle(Vector3.down, ropeDir);

        if (angle > freeEndWarningAngle && !isShowing)
            ShowWarning(freeEndWarningMessage);
    }

    // ©¤©¤ Connecting Rope Angle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void CheckConnectingRopeAngle()
    {
        if (pulleySystem == null) return;
        if (pulleySystem.cachedFixedSlot == null) return;

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

    public void ShowWarning(string message)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(ShowAndFade(message));
    }

    private IEnumerator ShowAndFade(string message)
    {
        isShowing = true;
        warningText.text = message;
        canvasGroup.alpha = 1f;

        // Text fully opaque, background semi-transparent
        warningText.alpha = 1f;
        SetBackgroundAlpha(backgroundAlpha);

        yield return new WaitForSeconds(displayDuration);

        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / fadeDuration);
            warningText.alpha = t;
            SetBackgroundAlpha(backgroundAlpha * t);
            yield return null;
        }

        warningText.alpha = 0f;
        SetBackgroundAlpha(0f);
        canvasGroup.alpha = 0f;
        isShowing = false;
    }

    // ©¤©¤ Helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void SetBackgroundAlpha(float alpha)
    {
        if (background == null) return;
        Color c = background.color;
        c.a = alpha;
        background.color = c;
    }

    private Transform FindChildByName(Transform parent, string partialName)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>())
            if (t.name.Contains(partialName)) return t;
        return null;
    }
}