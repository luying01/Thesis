using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class RopeGrab : MonoBehaviour
{
    [Header("Grab Settings")]
    public float moveRadius = 0.4f;

    [Header("References")]
    public PulleyPhysics pulleyPhysics;

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
            return;
        }

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
    }
}