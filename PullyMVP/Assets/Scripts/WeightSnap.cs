using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class WeightSnap : MonoBehaviour
{
    public float snapDistance = 0.08f;

    private XRGrabInteractable grabInteractable;
    private PulleyPhysics pulleyPhysics;
    public bool isSnapped = false;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        pulleyPhysics = FindObjectOfType<PulleyPhysics>();
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.selectEntered.AddListener(args => OnGrabbed());
    }

    void OnReleased(SelectExitEventArgs args)
    {
        if (isSnapped) return;
        StartCoroutine(TrySnapDelayed());
    }

    System.Collections.IEnumerator TrySnapDelayed()
    {
        yield return null;
        yield return null;

        if (TrySnapToWeight()) yield break;

        TrySnapToHook(pulleyPhysics.hookLeft, true);
        TrySnapToHook(pulleyPhysics.hookRight, false);
    }

    // Calculate world position so that this weight's Top aligns with the target point
    Vector3 GetPositionForTopAlignment(Vector3 targetWorldPos, Quaternion weightRotation)
    {
        Transform topPoint = transform.Find("weight_AttachPoint_Top");
        if (topPoint == null)
            return targetWorldPos + Vector3.down * 0.029f;

        // Local offset from weight center to Top point
        Vector3 localTopOffset = topPoint.localPosition;
        // Rotate the offset by the weight's intended rotation
        Vector3 worldTopOffset = weightRotation * localTopOffset;
        // Weight center = target - worldTopOffset
        return targetWorldPos - worldTopOffset;
    }

    void TrySnapToHook(Transform hook, bool isLeft)
    {
        if (hook == null || isSnapped) return;

        if (isLeft && pulleyPhysics.weightChainLeft != null) return;
        if (!isLeft && pulleyPhysics.weightChainRight != null) return;

        float dist = Vector3.Distance(transform.position, hook.position);
        if (dist < snapDistance)
        {
            // Hook: no rotation, Top aligns with hook
            Quaternion targetRotation = Quaternion.identity;
            Vector3 targetPosition = GetPositionForTopAlignment(hook.position, targetRotation);

            transform.SetParent(hook);
            transform.rotation = targetRotation;
            transform.position = targetPosition;

            if (isLeft)
                pulleyPhysics.weightChainLeft = this.gameObject;
            else
                pulleyPhysics.weightChainRight = this.gameObject;

            pulleyPhysics.velocity = 0f;
            isSnapped = true;
        }
    }

    bool TrySnapToWeight()
    {
        WeightSnap[] allWeights = FindObjectsOfType<WeightSnap>();
        foreach (WeightSnap other in allWeights)
        {
            if (other == this) continue;
            if (!other.isSnapped) continue;

            Transform bottomPoint = other.transform.Find("weight_AttachPoint_Bottom");
            if (bottomPoint == null) continue;
            if (bottomPoint.childCount > 0) continue;

            float dist = Vector3.Distance(transform.position, bottomPoint.position);
            if (dist < snapDistance)
            {
                // Rotate 90бу relative to the weight above
                float parentY = other.transform.eulerAngles.y;
                Quaternion targetRotation = Quaternion.Euler(0f, parentY + 90f, 0f);
                Vector3 targetPosition = GetPositionForTopAlignment(bottomPoint.position, targetRotation);

                transform.SetParent(bottomPoint);
                transform.rotation = targetRotation;
                transform.position = bottomPoint.position + Vector3.down * (0.029f - 0.01f) + Vector3.forward * 0.005f;
                isSnapped = true;
                return true;
            }
        }
        return false;
    }

    public void OnGrabbed()
    {
        if (!isSnapped) return;
        isSnapped = false;
        transform.SetParent(null);

        if (pulleyPhysics.weightChainLeft == this.gameObject)
            pulleyPhysics.weightChainLeft = null;
        if (pulleyPhysics.weightChainRight == this.gameObject)
            pulleyPhysics.weightChainRight = null;
    }
}