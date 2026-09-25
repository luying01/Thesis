using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MovablePulleyLock : MonoBehaviour
{
    [Header("References")]
    public PulleySystem pulleySystem;
    public ExperimentConfigManager experimentConfigManager;
    public PulleyPhysics pulleyPhysics;
    [Tooltip("Tells the student why the experiment reset. Optional.")]
    public WarningUI warningUI;
    public ResetManager resetManager;

    [Header("Settings")]
    public float snapRange = 0.3f;
    public float dragRadius = 0.3f;
    [Tooltip("Off: only a real grab moves the pulley. On: holding the trigger " +
         "inside the pulley's collider also drags it, which fires by " +
         "accident while pulling the rope past it.")]
    public bool allowTriggerDrag = false;
    [Tooltip("Message shown when the pulley is released off the rope.")]
    public string offRopeWarning = "The movable pulley must hang on the rope";

    [Header("Read Only")]
    public bool isLocked = false;

    private XRGrabInteractable grabInteractable;
    private bool wasSelected = false;

    public bool isDragging = false;
    private Transform draggingController = null;
    private Vector3 dragOrigin;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void Update()
    {
        bool isSelected = grabInteractable.isSelected;

        if (!wasSelected && isSelected && isLocked)
            Unlock();

        if (wasSelected && !isSelected && !isLocked)
        {
            PulleySlot nearestSlot = pulleySystem.GetNearestOccupiedFixedSlot(transform.position);
            if (nearestSlot != null)
            {
                float xDist = Mathf.Abs(transform.position.x - nearestSlot.transform.position.x);
                float dist = Vector3.Distance(transform.position, nearestSlot.transform.position);
                if (xDist <= 0.05f && dist <= snapRange)
                    AlignToFixedPulley(nearestSlot);
                else
                {
                    Debug.Log("[MovablePulleyLock] Out of snap range");
                    HandleOutOfRangeRelease();
                }
            }
            else
            {
                Debug.Log("[MovablePulleyLock] No fixed slot found");
                HandleOutOfRangeRelease();
            }
        }
        wasSelected = isSelected;

        UpdateTriggerDrag();
    }

    /// <summary>
    /// Released off the rope: warn, then put everything back to the loose,
    /// unassembled state the question started in - not to the question's
    /// configured layout. ResetToCurrentConfig re-applies that layout, which at
    /// high fidelity is the assembled arrangement the student is supposed to
    /// build themselves, so it left weights hanging mid-air on a rope that was
    /// not connected.
    ///
    /// Only reachable at high fidelity: at low fidelity the pulley is locked
    /// automatically and never released by hand.
    /// </summary>
    private void HandleOutOfRangeRelease()
    {
        if (warningUI != null)
            warningUI.ShowWarning(offRopeWarning);

        if (pulleySystem != null)
            pulleySystem.ResetToDefaultState();

        if (resetManager != null)
            resetManager.ResetAll();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!allowTriggerDrag) return;
        if (!isLocked) return;
        if (isDragging) return;
        if (pulleyPhysics == null) return;

        XRBaseController controller = other.GetComponentInParent<XRBaseController>();
        if (controller == null) return;

        ActionBasedController actionController = controller as ActionBasedController;
        if (actionController != null && actionController.activateAction.action.IsPressed())
        {
            isDragging = true;
            draggingController = other.transform;
            dragOrigin = pulleyPhysics.GetMovablePulleyPosition();
            pulleyPhysics.SetLoadGrabbed(true);
        }
    }

    private void UpdateTriggerDrag()
    {
        if (!isDragging || draggingController == null) return;

        if (!isLocked)
        {
            StopDrag();
            return;
        }

        XRBaseController controller = draggingController.GetComponentInParent<XRBaseController>();
        ActionBasedController actionController = controller as ActionBasedController;

        if (actionController == null || !actionController.activateAction.action.IsPressed())
        {
            StopDrag();
            return;
        }

        Vector3 target = draggingController.position;
        Vector3 offset = target - dragOrigin;
        if (offset.magnitude > dragRadius)
            offset = offset.normalized * dragRadius;

        Vector3 desiredPosition = dragOrigin + offset;
        pulleyPhysics.DragLoadTo(desiredPosition);
    }

    private void StopDrag()
    {
        isDragging = false;
        draggingController = null;
        if (pulleyPhysics != null)
            pulleyPhysics.SetLoadGrabbed(false);
    }

    private void AlignToFixedPulley(PulleySlot slot)
    {
        DoLock(slot);
    }

    // Public entry point for automated (non-grab) locking, used by
    // ExperimentConfigManager during low-fidelity auto-equip.
    public void AutoLockToFixedPulley(PulleySlot slot)
    {
        if (slot == null) return;
        DoLock(slot);
    }

    private void DoLock(PulleySlot slot)
    {
        Vector3 correctedPosition = new Vector3(
            slot.transform.position.x,
            transform.position.y,
            transform.position.z
        );
        transform.position = correctedPosition;
        transform.rotation = slot.transform.rotation;
        isLocked = true;
        pulleySystem.OnMovablePulleyLocked(this.transform);
        Debug.Log("Movable pulley locked and rope updated at " + transform.position);
    }

    private void Unlock()
    {
        StopDrag();
        isLocked = false;
        pulleySystem.OnMovablePulleyUnlocked();
        Debug.Log("Movable pulley UNLOCKED");
    }
}