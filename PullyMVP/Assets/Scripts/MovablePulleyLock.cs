using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class MovablePulleyLock : MonoBehaviour
{
    [Header("References")]
    public PulleySystem pulleySystem;

    [Header("Settings")]
    public float snapRange = 0.3f;

    [Header("Read Only")]
    public bool isLocked = false;

    private XRGrabInteractable grabInteractable;
    private bool wasSelected = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void Update()
    {
        bool isSelected = grabInteractable.isSelected;

        // Auto unlock when player grabs the pulley again
        if (!wasSelected && isSelected && isLocked)
            Unlock();

        // Detect when player releases the pulley
        if (wasSelected && !isSelected && !isLocked)
        {
            PulleySlot nearestSlot = pulleySystem.GetNearestOccupiedFixedSlot(transform.position);
            if (nearestSlot != null)
            {
                float xDist = Mathf.Abs(transform.position.x - nearestSlot.transform.position.x);
                if (xDist <= 0.05f)
                    AlignToFixedPulley(nearestSlot);
                else
                    Debug.Log("Too far from X plane to snap: " + xDist);
            }
        }

        wasSelected = isSelected;
    }

    private void AlignToFixedPulley(PulleySlot slot)
    {
        Vector3 correctedPosition = new Vector3(
            slot.transform.position.x,
            transform.position.y,
            transform.position.z
        );
        transform.position = correctedPosition;
        transform.rotation = slot.transform.rotation;
        isLocked = true;

        // Auto trigger rope update
        pulleySystem.OnMovablePulleyLocked(this.transform);
        Debug.Log("Movable pulley locked and rope updated at " + transform.position);
    }

    private void Unlock()
    {
        isLocked = false;
        pulleySystem.OnMovablePulleyUnlocked();
        Debug.Log("Movable pulley UNLOCKED");
    }
}