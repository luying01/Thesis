using UnityEngine;

public class PulleySlot : MonoBehaviour
{
    [Header("Slot Settings")]
    public bool isFront = true;          // true = front slot, false = back slot
    public bool isOccupied = false;      // is a pulley snapped here?
    public Transform snapPoint;          // exact position pulley snaps to (this transform)

    [Header("References")]
    public PulleySystem pulleySystem;    // assign in Inspector

    private GameObject snappedPulley;   // the pulley currently in this slot

    public GameObject snappedPulleyObject;  // the actual pulley GameObject snapped here

    private void Start()
    {
        snapPoint = this.transform;
    }

    // Called when a pulley enters the trigger zone
    private void OnTriggerEnter(Collider other)
    {
        if (isOccupied) return;
        if (other.CompareTag("Pulley"))
        {
            // Highlight to show snapping is available
            other.gameObject.GetComponent<PulleyObject>()?.ShowSnapHighlight(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Pulley"))
        {
            other.gameObject.GetComponent<PulleyObject>()?.ShowSnapHighlight(false);
        }
    }

    // Called by PulleyObject when released inside this trigger
    public void SnapPulley(GameObject pulley)
    {
        if (isOccupied) return;
        isOccupied = true;
        snappedPulley = pulley;
        snappedPulleyObject = pulley;  // add this line
        pulley.transform.position = snapPoint.position;
        pulley.transform.rotation = snapPoint.rotation;
        pulleySystem?.OnPulleyPlaced(this);
    }

    // Called by PulleyObject when grabbed out of this slot
    public void ReleasePulley()
    {
        isOccupied = false;
        snappedPulley = null;

        // Notify PulleySystem to recalculate rope
        pulleySystem?.OnPulleyRemoved(this);
    }
}