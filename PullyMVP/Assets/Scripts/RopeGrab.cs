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
    private Vector3 originPosition;

    private void Start()
    {
        originPosition = transform.position;
    }

    private void OnTriggerStay(Collider other)
    {
        if (isGrabbed) return;

        XRBaseController controller = other.GetComponentInParent<XRBaseController>();
        if (controller == null) return;

        ActionBasedController actionController = controller as ActionBasedController;
        if (actionController != null && actionController.activateAction.action.IsPressed())
        {
            isGrabbed = true;
            grabbingController = other.transform;

            // Auto-detect which controller is grabbing for haptics
            hapticController = actionController;
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

        // Move hook logic
        Vector3 target = grabbingController.position;
        Vector3 offset = target - originPosition;
        if (offset.magnitude > moveRadius)
        {
            offset = offset.normalized * moveRadius;
        }

        Vector3 desiredPosition = originPosition + offset;

        if (pulleyPhysics != null)
        {
            bool isLeft = (pulleyPhysics.hookLeft == this.transform);
            Transform mySlot = isLeft ? pulleyPhysics.slotLeft : pulleyPhysics.slotRight;
            float maxAllowedLength = pulleyPhysics.totalRopeLength - 0.05f;

            Vector3 slotToDesired = desiredPosition - mySlot.position;
            if (slotToDesired.magnitude > maxAllowedLength)
            {
                desiredPosition = mySlot.position + slotToDesired.normalized * maxAllowedLength;
            }

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

        float massLeft = pulleyPhysics.massLeft;
        float massRight = pulleyPhysics.massRight;
        float g = 9.81f;

        float amplitude = 0f;

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
            {
                tension = (2f * massLeft * massRight * g) / (massLeft + massRight);
            }
            else
            {
                tension = Mathf.Max(massLeft, massRight) * g;
            }

            // Calibrated for 25g weights (0.025kg each)
            // 1 weight (0.025kg): ~0.25N  -> low amplitude
            // 4 weights (0.1kg):  ~1.0N   -> near max amplitude
            float minTension = 0.025f * g;
            float maxTension = 0.1f * g;
            amplitude = Mathf.InverseLerp(minTension, maxTension, tension) * maxHapticAmplitude;
            amplitude = Mathf.Clamp(amplitude, 0.2f, maxHapticAmplitude);
        }

        if (amplitude > 0f)
        {
            hapticController.SendHapticImpulse(amplitude, 0.1f);
        }
    }
}