using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class RopeGrab : MonoBehaviour
{
    [Header("Grab Settings")]
    public float moveRadius = 0.4f;

    [Header("References")]
    public PulleyPhysics pulleyPhysics;

    [Header("Haptic Settings")]
    public ActionBasedController hapticController; // drag the XR controller here in Inspector
    public float maxHapticAmplitude = 0.8f;
    public float maxReferenceMass = 10f; // total mass reference for normalization

    public bool isGrabbed = false;
    private Transform grabbingController = null;

    // Both recorded at the start of each grab, not once at scene load.
    //
    // The hook moves BY how far the hand has moved since the trigger went down,
    // not TO wherever the hand currently is. Driving it to the hand's position
    // made the hook jump the instant the trigger was pressed, by however far the
    // hand happened to be from it - and anchoring the movement sphere to the
    // scene-load position left that sphere up where the hook began, so once
    // weights had pulled the hook down, grabbing yanked it back up.
    private Vector3 originPosition;
    private Vector3 grabStartControllerPosition;

    private void OnTriggerStay(Collider other)
    {
        if (isGrabbed) return;

        XRBaseController controller = other.GetComponentInParent<XRBaseController>();
        if (controller == null) return;

        ActionBasedController actionController = controller as ActionBasedController;
        if (actionController != null && actionController.activateAction.action.IsPressed())
        {
            originPosition = transform.position;
            grabStartControllerPosition = other.transform.position;
            isGrabbed = true;
            grabbingController = other.transform;

            // Auto-detect which controller is grabbing for haptics
            hapticController = actionController;

            // Start distance tracking the first time this hook is ever grabbed,
            // so the conceptual aid ruler works even when no weight is attached
            if (pulleyPhysics != null)
            {
                bool isThisHookLeft = pulleyPhysics.hookLeft == this.transform;
                bool alreadyTracking = isThisHookLeft
                    ? pulleyPhysics.IsTrackingHookL()
                    : pulleyPhysics.IsTrackingHookR();

                if (!alreadyTracking)
                {
                    string endpointName = isThisHookLeft ? "HookL" : "HookR";
                    pulleyPhysics.StartLoadTracking(endpointName, transform.position);
                }
            }
        }
    }

    private void Update()
    {
        if (!isGrabbed || grabbingController == null) return;

        XRBaseController controller = grabbingController.GetComponentInParent<XRBaseController>();
        ActionBasedController actionController = controller as ActionBasedController;

        if (actionController != null && !actionController.activateAction.action.IsPressed())
        {
            isGrabbed = false;
            grabbingController = null;
            hapticController = null;
            return;
        }

        // Move hook logic - relative to where the hand was when the grab began.
        Vector3 handDelta = grabbingController.position - grabStartControllerPosition;

        Vector3 desiredPosition = originPosition + handDelta;

        if (pulleyPhysics != null)
        {
            bool isLeft = (pulleyPhysics.hookLeft == this.transform);
            Transform mySlot = isLeft ? pulleyPhysics.slotLeft : pulleyPhysics.slotRight;

            // Which point the budget is measured from matters. On a movable
            // pulley rig the solver derives the free segment from the fixed
            // pulley slot, so clamping against the near slot measures a
            // different distance and cuts the pull short.
            //
            // The budget itself is fixed - the whole rope minus the two pulley
            // segments at their closest approach - rather than tracking the
            // current d1. And it has to exist: while the free end is held, the
            // solver reads d2 off the hook and only clamps d1, so nothing there
            // stops the hook from running away and stretching the rope.
            Transform anchor = mySlot;
            float maxAllowedLength = pulleyPhysics.totalRopeLength - 0.05f;

            if (pulleyPhysics.IsMovablePulleyConfig())
            {
                Transform fixedRef = pulleyPhysics.GetFixedPulleySlotRef();
                if (fixedRef != null)
                {
                    anchor = fixedRef;
                    maxAllowedLength = pulleyPhysics.totalRopeLengthMA2
                                     - pulleyPhysics.minD1 * 2f;
                }
            }

            Vector3 anchorToDesired = desiredPosition - anchor.position;
            if (anchorToDesired.magnitude > maxAllowedLength)
                desiredPosition = anchor.position + anchorToDesired.normalized * maxAllowedLength;

            float desiredLength = Vector3.Distance(mySlot.position, desiredPosition);
            if (desiredLength < 0.05f) return;
        }

        transform.position = desiredPosition;

        // Send haptic feedback every frame while grabbing
        SendHaptics();
    }

    private void SendHaptics()
    {
        if (hapticController == null || pulleyPhysics == null) return;

        float g = 9.81f;
        float amplitude = 0f;

        bool isMA2 = pulleyPhysics.IsMovablePulleyConfig();

        if (isMA2)
        {
            // Movable pulley config: use tension force from physics
            float tension = pulleyPhysics.tensionForce;
            if (tension > 0f)
            {
                float minTension = 0.025f * g;
                float maxTension = 0.2f * g;
                amplitude = Mathf.InverseLerp(minTension, maxTension, tension) * maxHapticAmplitude;
                amplitude = Mathf.Clamp(amplitude, 0.2f, maxHapticAmplitude);
            }
        }
        else
        {
            // Atwood config: use mass-based tension calculation
            float massLeft = pulleyPhysics.massLeft;
            float massRight = pulleyPhysics.massRight;

            bool isLeft = (pulleyPhysics.hookLeft == this.transform);
            float myLength = isLeft ? pulleyPhysics.leftLength : pulleyPhysics.rightLength;

            if (myLength <= 0.05f)
            {
                amplitude = maxHapticAmplitude;
            }
            else if (massLeft > 0f || massRight > 0f)
            {
                float tension = 0f;
                if (massLeft > 0f && massRight > 0f)
                    tension = (2f * massLeft * massRight * g) / (massLeft + massRight);
                else
                    tension = Mathf.Max(massLeft, massRight) * g;

                float minTension = 0.025f * g;
                float maxTension = 0.1f * g;
                amplitude = Mathf.InverseLerp(minTension, maxTension, tension) * maxHapticAmplitude;
                amplitude = Mathf.Clamp(amplitude, 0.2f, maxHapticAmplitude);
            }
        }

        if (amplitude > 0f)
            hapticController.SendHapticImpulse(amplitude, 0.1f);
    }
}