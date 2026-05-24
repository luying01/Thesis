using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class WeightSnap : MonoBehaviour
{
    public float snapDistance = 0.08f;

    private XRGrabInteractable grabInteractable;
    private PulleyPhysics pulleyPhysics;
    public bool isSnapped = false;

    
    private ActionBasedController currentController = null;
   

    // Calibrated for 25g to 100g weights
    // At min mass: amplitude 0.2, interval 0.3s
    // At max mass: amplitude 0.7, interval 0.05s
    private float minMass = 0.025f;
    private float maxMass = 0.4f; // adjust if you add more weights
    private float minAmplitude = 0.2f;
    private float maxAmplitude = 0.7f;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        pulleyPhysics = FindObjectOfType<PulleyPhysics>();
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.selectEntered.AddListener(OnGrabbedWithArgs);
    }

    void Update()
    {
        // Continuous haptic while holding the weight
        if (currentController == null) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        float mass = rb != null ? rb.mass : minMass;

        // Normalize mass to amplitude, calibrated for 25g to 400g
        float t = Mathf.InverseLerp(minMass, maxMass, mass);
        float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);
        amplitude = Mathf.Clamp(amplitude, minAmplitude, maxAmplitude);

        // Send every frame with short duration, same pattern as RopeGrab
        currentController.SendHapticImpulse(amplitude, 0.1f);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        // Stop haptics
        currentController = null;

        if (isSnapped) return;
        StartCoroutine(TrySnapDelayed());
    }

    void OnGrabbedWithArgs(SelectEnterEventArgs args)
    {
        // Start tracking which controller is holding this weight
        currentController = args.interactorObject.transform
            .GetComponentInParent<ActionBasedController>();

        OnGrabbed();
    }

    System.Collections.IEnumerator TrySnapDelayed()
    {
        yield return null;
        yield return null;

        if (TrySnapToWeight()) yield break;

        TrySnapToHook(pulleyPhysics.hookLeft, true);
        TrySnapToHook(pulleyPhysics.hookRight, false);
    }

    Vector3 GetPositionForTopAlignment(Vector3 targetWorldPos, Quaternion weightRotation)
    {
        Transform topPoint = transform.Find("weight_AttachPoint_Top");
        if (topPoint == null)
            return targetWorldPos + Vector3.down * 0.029f;

        Vector3 localTopOffset = topPoint.localPosition;
        Vector3 worldTopOffset = weightRotation * localTopOffset;
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
                float parentY = other.transform.eulerAngles.y;
                Quaternion targetRotation = Quaternion.Euler(0f, parentY + 90f, 0f);

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