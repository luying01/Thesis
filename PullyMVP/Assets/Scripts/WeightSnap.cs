using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class WeightSnap : MonoBehaviour
{
    public float snapDistance = 0.08f;

    private XRGrabInteractable grabInteractable;
    private PulleyPhysics pulleyPhysics;
    public bool isSnapped = false;

    private ActionBasedController currentController = null;

    private float minMass = 0.025f;
    private float maxMass = 0.4f;
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
        if (currentController == null) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        float mass = rb != null ? rb.mass : minMass;

        float t = Mathf.InverseLerp(minMass, maxMass, mass);
        float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);
        amplitude = Mathf.Clamp(amplitude, minAmplitude, maxAmplitude);

        currentController.SendHapticImpulse(amplitude, 0.1f);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        currentController = null;
        if (isSnapped) return;
        StartCoroutine(TrySnapDelayed());
    }

    void OnGrabbedWithArgs(SelectEnterEventArgs args)
    {
        currentController = args.interactorObject.transform
            .GetComponentInParent<ActionBasedController>();
        OnGrabbed();
    }

    System.Collections.IEnumerator TrySnapDelayed()
    {
        yield return null;
        yield return null;

        // Try snapping to another weight first
        if (TrySnapToWeight()) yield break;

        // Try snapping to movable pulley bottom (load side)
        if (TrySnapToMovablePulleyBottom()) yield break;

        // Try snapping to hooks (free end or fixed side)
        TrySnapToHook(pulleyPhysics.hookLeft, true);
        TrySnapToHook(pulleyPhysics.hookRight, false);
    }

    bool TrySnapToMovablePulleyBottom()
    {
        if (pulleyPhysics.movablePulleyBottom == null) return false;
        if (pulleyPhysics.weightChainLoad != null) return false;

        Transform bottomPoint = pulleyPhysics.movablePulleyBottom;
        float dist = Vector3.Distance(transform.position, bottomPoint.position);

        if (dist < snapDistance)
        {
            Quaternion targetRotation = Quaternion.identity;
            Vector3 targetPosition = GetPositionForTopAlignment(bottomPoint.position, targetRotation);

            transform.SetParent(bottomPoint);
            transform.rotation = targetRotation;
            transform.position = targetPosition;

            pulleyPhysics.weightChainLoad = this.gameObject;
            pulleyPhysics.velocity = 0f;

            // Init loadY
            pulleyPhysics.GetType()
                .GetField("loadY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(pulleyPhysics, bottomPoint.position.y);

            isSnapped = true;
            Debug.Log("Weight snapped to movable pulley bottom");
            return true;
        }
        return false;
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

        float MA = pulleyPhysics.pulleySystem != null ?
                   pulleyPhysics.pulleySystem.GetMechanicalAdvantage() : 1f;

        float dist = Vector3.Distance(transform.position, hook.position);
        if (dist < snapDistance)
        {
            Quaternion targetRotation = Quaternion.identity;
            Vector3 targetPosition = GetPositionForTopAlignment(hook.position, targetRotation);

            transform.SetParent(hook);
            transform.rotation = targetRotation;
            transform.position = targetPosition;

            if (MA > 1f)
            {
                // MA=2: free end hook ¡ú weightChainForce
                if (pulleyPhysics.weightChainForce == null)
                    pulleyPhysics.weightChainForce = this.gameObject;
            }
            else
            {
                // MA=1: standard Atwood
                if (isLeft && pulleyPhysics.weightChainLeft == null)
                    pulleyPhysics.weightChainLeft = this.gameObject;
                else if (!isLeft && pulleyPhysics.weightChainRight == null)
                    pulleyPhysics.weightChainRight = this.gameObject;
            }

            pulleyPhysics.velocity = 0f;
            isSnapped = true;
            Debug.Log("Weight snapped to hook: " + hook.name + " MA=" + MA);
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

        if (pulleyPhysics.weightChainLeft == this.gameObject) pulleyPhysics.weightChainLeft = null;
        if (pulleyPhysics.weightChainRight == this.gameObject) pulleyPhysics.weightChainRight = null;
        if (pulleyPhysics.weightChainLoad == this.gameObject) pulleyPhysics.weightChainLoad = null;
        if (pulleyPhysics.weightChainForce == this.gameObject) pulleyPhysics.weightChainForce = null;
    }
}