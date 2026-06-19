using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class PulleyObject : MonoBehaviour
{
    [Header("Settings")]
    public bool isFixed = true;          // true = fixed pulley, false = movable pulley

    private PulleySlot currentSlot;      // slot this pulley is snapped to
    private XRGrabInteractable grabInteractable;
    private Renderer pulleyRenderer;
    private Color originalColor;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        pulleyRenderer = GetComponentInChildren<Renderer>();
        if (pulleyRenderer != null)
            originalColor = pulleyRenderer.material.color;

        // Listen to grab and release events
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    // Called when player releases the pulley
    private void OnReleased(SelectExitEventArgs args)
    {
        // Check if overlapping with any slot
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.05f);
        foreach (var hit in hits)
        {
            PulleySlot slot = hit.GetComponent<PulleySlot>();
            if (slot != null && !slot.isOccupied)
            {
                // Release from old slot if any
                currentSlot?.ReleasePulley();

                // Snap to new slot
                currentSlot = slot;
                slot.SnapPulley(gameObject);
                ShowSnapHighlight(false);
                return;
            }
        }

        // No slot found - release from current slot
        currentSlot?.ReleasePulley();
        currentSlot = null;
    }

    // Show/hide highlight when near a slot
    public void ShowSnapHighlight(bool show)
    {
        if (pulleyRenderer == null) return;
        pulleyRenderer.material.color = show ? Color.yellow : originalColor;
    }
}