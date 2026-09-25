using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class WeightSnap : MonoBehaviour
{
    public float snapDistance = 0.08f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private PulleyPhysics pulleyPhysics;
    public bool isSnapped = false;

    private ActionBasedController currentController = null;

    private float minMass = 0.025f;
    private float maxMass = 0.4f;
    private float minAmplitude = 0.2f;
    private float maxAmplitude = 0.7f;

    void Start()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
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

        if (TrySnapToWeight()) yield break;
        if (TrySnapToMovablePulleyBottom()) yield break;

        TrySnapToHook(pulleyPhysics.hookLeft, true);
        TrySnapToHook(pulleyPhysics.hookRight, false);
    }

    // ©¤©¤ Movable Pulley Bottom ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    bool TrySnapToMovablePulleyBottom()
    {
        if (pulleyPhysics.movablePulleyBottom == null) return false;
        if (pulleyPhysics.weightChainLoad != null) return false;

        Transform bottomPoint = pulleyPhysics.movablePulleyBottom;
        float dist = Vector3.Distance(transform.position, bottomPoint.position);

        if (dist < snapDistance)
        {
            DoSnapToMovablePulleyBottom(bottomPoint);
            return true;
        }
        return false;
    }

    // Public entry point for automated (non-grab) placement, e.g. low-fidelity auto-equip
    public void AutoSnapToMovablePulleyBottom()
    {
        if (isSnapped) return;
        if (pulleyPhysics == null) pulleyPhysics = FindObjectOfType<PulleyPhysics>();
        if (pulleyPhysics.movablePulleyBottom == null) return;
        if (pulleyPhysics.weightChainLoad != null) return;

        DoSnapToMovablePulleyBottom(pulleyPhysics.movablePulleyBottom);
    }

    private void DoSnapToMovablePulleyBottom(Transform bottomPoint)
    {
        Quaternion targetRotation = Quaternion.identity;
        Vector3 targetPosition = GetPositionForTopAlignment(bottomPoint.position, targetRotation);

        transform.SetParent(bottomPoint);
        transform.rotation = targetRotation;
        transform.position = targetPosition;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        pulleyPhysics.weightChainLoad = this.gameObject;
        pulleyPhysics.velocity = 0f;

        pulleyPhysics.GetType()
            .GetField("loadY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(pulleyPhysics, bottomPoint.position.y);

        pulleyPhysics.StartLoadTracking("MovablePulley", bottomPoint.position);

        isSnapped = true;
        Debug.Log("Weight snapped to movable pulley bottom");
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

    // ©¤©¤ Hooks ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    void TrySnapToHook(Transform hook, bool isLeft)
    {
        if (hook == null || isSnapped) return;

        float dist = Vector3.Distance(transform.position, hook.position);
        if (dist < snapDistance)
            DoSnapToHook(hook, isLeft);
    }

    // Public entry point for automated (non-grab) placement, e.g. low-fidelity auto-equip
    public void AutoSnapToHook(Transform hook, bool isLeft)
    {
        if (hook == null || isSnapped) return;
        if (pulleyPhysics == null) pulleyPhysics = FindObjectOfType<PulleyPhysics>();
        DoSnapToHook(hook, isLeft);
    }

    private void DoSnapToHook(Transform hook, bool isLeft)
    {
        float MA = pulleyPhysics.pulleySystem != null ?
                   pulleyPhysics.pulleySystem.GetMechanicalAdvantage() : 1f;

        Quaternion targetRotation = Quaternion.identity;
        Vector3 targetPosition = GetPositionForTopAlignment(hook.position, targetRotation);

        transform.SetParent(hook);
        transform.rotation = targetRotation;
        transform.position = targetPosition;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (MA > 1f)
        {
            if (pulleyPhysics.weightChainForce == null)
            {
                pulleyPhysics.weightChainForce = this.gameObject;

                bool hookIsFreeEndLeft = (hook == pulleyPhysics.hookLeft);
                string endpointName = hookIsFreeEndLeft ? "HookL" : "HookR";
                pulleyPhysics.StartLoadTracking(endpointName, hook.position);
            }
        }
        else
        {
            if (isLeft && pulleyPhysics.weightChainLeft == null)
            {
                pulleyPhysics.weightChainLeft = this.gameObject;
                pulleyPhysics.StartLoadTracking("HookL", hook.position);
            }
            else if (!isLeft && pulleyPhysics.weightChainRight == null)
            {
                pulleyPhysics.weightChainRight = this.gameObject;
                pulleyPhysics.StartLoadTracking("HookR", hook.position);
            }
        }

        pulleyPhysics.velocity = 0f;
        isSnapped = true;
        Debug.Log("Weight snapped to hook: " + hook.name + " MA=" + MA);
    }

    // ©¤©¤ Weight-to-Weight Chaining ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

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
                DoSnapOntoWeight(other);
                return true;
            }
        }
        return false;
    }

    // Public entry point for automated (non-grab) chaining onto a specific
    // already-placed weight, e.g. low-fidelity auto-equip stacking multiple weights.
    public void AutoSnapOntoWeight(WeightSnap previous)
    {
        if (isSnapped) return;
        if (previous == null) return;

        Transform bottomPoint = previous.transform.Find("weight_AttachPoint_Bottom");
        if (bottomPoint == null) return;
        if (bottomPoint.childCount > 0) return;

        DoSnapOntoWeight(previous);
    }

    private void DoSnapOntoWeight(WeightSnap other)
    {
        Transform bottomPoint = other.transform.Find("weight_AttachPoint_Bottom");
        if (bottomPoint == null) return;

        // Each weight in a stack sits a quarter turn from the one above it, the
        // way real slotted masses are laid up.
        float parentY = other.transform.eulerAngles.y;
        Quaternion targetRotation = Quaternion.Euler(0f, parentY + 90f, 0f);

        // Position the same way the hook and movable-pulley snaps do: line this
        // weight's own top attach point up with the target point, with the
        // offset rotated by the weight's own rotation.
        //
        // The old hard-coded drop could not do that. It assumed a fixed vertical
        // gap regardless of orientation, so once each weight turned 90 degrees
        // the alignment no longer matched - which is what the small sideways
        // nudge was there to paper over, and that nudge then accumulated down
        // the chain because every weight is parented to the one above it.
        Vector3 targetPosition = GetPositionForTopAlignment(bottomPoint.position, targetRotation);

        transform.SetParent(bottomPoint);
        transform.rotation = targetRotation;
        transform.position = targetPosition;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        isSnapped = true;
    }

    // ©¤©¤ Dispatcher used by ExperimentConfigManager for the FIRST weight on a target ©¤©¤

    public void AutoSnapToTarget(string target)
    {
        if (isSnapped) return;
        if (string.IsNullOrEmpty(target)) return;
        if (pulleyPhysics == null) pulleyPhysics = FindObjectOfType<PulleyPhysics>();

        switch (target)
        {
            case "Hook_L":
                AutoSnapToHook(pulleyPhysics.hookLeft, true);
                break;
            case "Hook_R":
                AutoSnapToHook(pulleyPhysics.hookRight, false);
                break;
            case "MovablePulley":
                AutoSnapToMovablePulleyBottom();
                break;
        }
    }

    // ©¤©¤ Grip Release (unchanged) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void OnGrabbed()
    {
        if (!isSnapped) return;
        isSnapped = false;
        transform.SetParent(null);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        if (pulleyPhysics.weightChainLeft == this.gameObject) pulleyPhysics.weightChainLeft = null;
        if (pulleyPhysics.weightChainRight == this.gameObject) pulleyPhysics.weightChainRight = null;
        if (pulleyPhysics.weightChainLoad == this.gameObject) pulleyPhysics.weightChainLoad = null;
        if (pulleyPhysics.weightChainForce == this.gameObject) pulleyPhysics.weightChainForce = null;
    }

    // Used by demo sequence playback to instantly detach without needing a
    // physical grab event, so the weight can be animated back to storage.
    public void ForceDetach()
    {
        isSnapped = false;
        transform.SetParent(null);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (pulleyPhysics.weightChainLeft == this.gameObject) pulleyPhysics.weightChainLeft = null;
        if (pulleyPhysics.weightChainRight == this.gameObject) pulleyPhysics.weightChainRight = null;
        if (pulleyPhysics.weightChainLoad == this.gameObject) pulleyPhysics.weightChainLoad = null;
        if (pulleyPhysics.weightChainForce == this.gameObject) pulleyPhysics.weightChainForce = null;
    }
}