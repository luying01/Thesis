using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PulleyObject : MonoBehaviour
{
    [Header("Settings")]
    public bool isFixed = true;

    [Header("References")]
    public ExperimentConfigManager experimentConfigManager;
    public FidelityManager fidelityManager;

    private PulleySlot currentSlot;
    private XRGrabInteractable grabInteractable;
    private Renderer pulleyRenderer;
    private Color originalColor;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        pulleyRenderer = GetComponentInChildren<Renderer>();
        if (pulleyRenderer != null)
            originalColor = pulleyRenderer.material.color;

        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    // Called when player grabs the pulley
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (!isFixed) return;
        if (experimentConfigManager == null) return;
        if (fidelityManager == null) return;

        int level = fidelityManager.GetCurrentLevel();
        bool isHighFidelity = (level == 2 || level == 3);

        // Only auto-reset in low fidelity; high fidelity allows free manual placement
        if (isHighFidelity) return;

        Debug.Log("[PulleyObject] Fixed pulley grabbed (low fidelity), resetting experiment");
        experimentConfigManager.ResetToCurrentConfig();
    }

    // Called when player releases the pulley
    private void OnReleased(SelectExitEventArgs args)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.05f);
        foreach (var hit in hits)
        {
            PulleySlot slot = hit.GetComponent<PulleySlot>();
            if (slot != null && !slot.isOccupied)
            {
                currentSlot?.ReleasePulley();
                currentSlot = slot;
                slot.SnapPulley(gameObject);
                ShowSnapHighlight(false);
                return;
            }
        }

        currentSlot?.ReleasePulley();
        currentSlot = null;
    }

    public void ShowSnapHighlight(bool show)
    {
        if (pulleyRenderer == null) return;
        pulleyRenderer.material.color = show ? Color.yellow : originalColor;
    }
}