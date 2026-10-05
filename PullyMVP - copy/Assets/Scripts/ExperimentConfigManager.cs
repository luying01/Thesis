using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ── Data Classes ──────────────────────────────────────────────────────────────

[System.Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[System.Serializable]
public class WeightAssignment
{
    public string weightName;   // e.g. "weight_100g_1"
    public string target;       // "Hook_L", "Hook_R", "MovablePulley"
}

[System.Serializable]
public class ExperimentConfig
{
    public string label;                        // "A", "B", "C"
    public bool useFixedPulley;
    public bool useMovablePulley;
    public int fixedPulleyHangerIndex;          // 0-4, used in low fidelity (Level 0)
    public Vector3Data movablePulleyOffset;     // Offset from fixed pulley
    public WeightAssignment[] weightAssignments;
    public bool holdOnly;
    public float holdSeconds;
}

// ── ExperimentConfigManager ──────────────────────────────────────────────────

public class ExperimentConfigManager : MonoBehaviour
{
    [Header("Scene Objects - Pulleys")]
    public GameObject fixedPulley;          // pully_fixed_S
    public GameObject movablePulley;        // pully_movable_S

    [Header("Scene Objects - Hooks")]
    public GameObject hookL;                // Hook_L
    public GameObject hookR;                // Hook_R

    [Header("Scene Objects - Weights")]
    public GameObject[] allWeights;         // All weight objects in scene

    [Header("Scene Objects - Hangers")]
    public Transform[] hangers;             // hanger to hanger.004, index 0-4

    [Header("Animation")]
    public float animationDuration = 1.0f;

    [Header("References")]
    public FidelityManager fidelityManager;
    public ResetManager resetManager;
    public PulleyPhysics pulleyPhysics;
    public ConceptualAidManager conceptualAidManager;

    // Current active config
    private ExperimentConfig currentConfig;

    // Active coroutines tracker
    private List<Coroutine> activeCoroutines = new List<Coroutine>();

    // Currently running demo/single-equip coroutine (Play button driven)
    private Coroutine currentDemoCoroutine;

    // ── Fidelity Level Change ─────────────────────────────────────────────

    public void OnFidelityLevelChanged(int newLevel)
    {
        if (currentConfig == null) return;
        ApplyConfig(currentConfig);
    }

    // ── Main Entry Point ─────────────────────────────────────────────────

    public void ApplyConfig(ExperimentConfig config)
    {
        currentConfig = config;

        targetWeightCount = new Dictionary<string, int>();
        lastAutoSnappedPerTarget.Clear();

        foreach (Coroutine c in activeCoroutines)
            if (c != null) StopCoroutine(c);
        activeCoroutines.Clear();

        ApplyVisibility(config);

        // Three-level ladder: Level 2 and 1 are high interaction fidelity,
        // Level 0 is low fidelity (system places the equipment).
        int level = fidelityManager != null ? fidelityManager.GetCurrentLevel() : FidelityManager.MaxLevel;
        bool isHighFidelity = (level == 1 || level == 2);

        if (isHighFidelity)
        {
            ApplyHighFidelity(config);
        }
        else
        {
            // Low fidelity no longer auto-equips. Just show the right equipment
            // (already done above via ApplyVisibility) and disable grabbing until
            // the student presses Play, which calls StartSingleEquip() or
            // StartDemoSequence() from DemoPlayButtonController.
            SetGrabbable(fixedPulley, false);
            SetGrabbable(movablePulley, false);
            SetGrabbable(hookL, false);
            SetGrabbable(hookR, false);
            foreach (WeightAssignment wa in config.weightAssignments)
            {
                GameObject weight = FindWeight(wa.weightName);
                if (weight != null) SetGrabbable(weight, false);
            }
        }
    }

    // ── Step 1: Visibility ────────────────────────────────────────────────

    private void ApplyVisibility(ExperimentConfig config)
    {
        fixedPulley.SetActive(config.useFixedPulley);
        movablePulley.SetActive(config.useMovablePulley);

        foreach (GameObject w in allWeights)
            w.SetActive(false);

        foreach (WeightAssignment wa in config.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight != null)
                weight.SetActive(true);
        }
    }

    // ── Step 2a: High Fidelity ────────────────────────────────────────────

    private void ApplyHighFidelity(ExperimentConfig config)
    {
        SetGrabbable(fixedPulley, true);
        SetGrabbable(movablePulley, true);
        SetGrabbable(hookL, true);
        SetGrabbable(hookR, true);

        foreach (WeightAssignment wa in config.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight != null)
                SetGrabbable(weight, true);
        }

        Debug.Log("[ExperimentConfigManager] High fidelity: all equipment grabbable");
    }

    // ── Step 2b: Low Fidelity (used both by legacy auto-path and Play button) ──

    private IEnumerator ApplyLowFidelity(ExperimentConfig config, string aidType)
    {
        SetGrabbable(fixedPulley, false);
        SetGrabbable(movablePulley, false);
        SetGrabbable(hookL, false);
        SetGrabbable(hookR, false);

        foreach (WeightAssignment wa in config.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight != null) SetGrabbable(weight, false);
        }

        PulleySlot targetSlot = null;

        if (config.useFixedPulley && config.fixedPulleyHangerIndex < hangers.Length)
        {
            Vector3 hangerTarget = hangers[config.fixedPulleyHangerIndex].position;
            yield return StartCoroutine(AnimateToPosition(fixedPulley, hangerTarget));

            targetSlot = hangers[config.fixedPulleyHangerIndex].GetComponentInChildren<PulleySlot>();
            if (targetSlot != null)
                targetSlot.SnapPulley(fixedPulley);
        }

        if (config.useMovablePulley)
        {
            Vector3 movableTarget = fixedPulley.transform.position
                                  + config.movablePulleyOffset.ToVector3();
            yield return StartCoroutine(AnimateToPosition(movablePulley, movableTarget));

            MovablePulleyLock mpl = movablePulley.GetComponent<MovablePulleyLock>();
            if (mpl != null && targetSlot != null)
                mpl.AutoLockToFixedPulley(targetSlot);
        }

        yield return StartCoroutine(GroupAndDeliverWeights(config));
        yield return StartCoroutine(MaybePlayForceReveal(aidType));

        foreach (WeightAssignment wa in config.weightAssignments)
        {
            if (!string.IsNullOrEmpty(wa.target)) continue;

            GameObject weight = FindWeight(wa.weightName);
            if (weight != null) SetGrabbable(weight, true);
        }

        Debug.Log("[ExperimentConfigManager] Low fidelity: animation complete");
    }

    private IEnumerator GroupAndDeliverWeights(ExperimentConfig config)
    {
        Dictionary<string, List<WeightAssignment>> groups = new Dictionary<string, List<WeightAssignment>>();
        foreach (WeightAssignment wa in config.weightAssignments)
        {
            if (string.IsNullOrEmpty(wa.target)) continue;
            if (!groups.ContainsKey(wa.target))
                groups[wa.target] = new List<WeightAssignment>();
            groups[wa.target].Add(wa);
        }

        List<(string target, WeightSnap head)> headsToRegister = new List<(string, WeightSnap)>();
        List<Coroutine> moves = new List<Coroutine>();

        foreach (var kvp in groups)
        {
            string target = kvp.Key;
            List<WeightAssignment> members = kvp.Value;

            GameObject headObj = FindWeight(members[0].weightName);
            if (headObj == null) continue;
            WeightSnap headSnap = headObj.GetComponent<WeightSnap>();

            // Build the stack as if the head were already in its final pose.
            // The head ends upright (identity) on the hook / movable pulley, and
            // every member is parented under it, so the head's turn from its
            // current rotation to identity is passed down the whole stack. If
            // the members were aligned to the head's CURRENT rotation (e.g. after
            // a previous run left it tilted), that turn tipped every member on
            // its side - the "Show Forces Again" bug. Aligning against identity
            // makes the result independent of where the weights were before.
            Quaternion headStartRotation = headObj.transform.rotation;
            headObj.transform.rotation = Quaternion.identity;

            WeightSnap previous = headSnap;
            for (int i = 1; i < members.Count; i++)
            {
                GameObject memberObj = FindWeight(members[i].weightName);
                if (memberObj == null) continue;
                WeightSnap memberSnap = memberObj.GetComponent<WeightSnap>();
                if (memberSnap == null || previous == null) continue;

                memberSnap.AutoSnapOntoWeight(previous);
                previous = memberSnap;
            }

            // Put the head back where it was visually (the stack turns with it
            // as one rigid piece), then let it turn upright during the move.
            headObj.transform.rotation = headStartRotation;

            Vector3 targetPos = GetWeightTargetPosition(target);
            moves.Add(StartCoroutine(AnimateToPose(headObj, targetPos, Quaternion.identity)));

            if (headSnap != null)
                headsToRegister.Add((target, headSnap));
        }

        foreach (Coroutine c in moves)
            yield return c;

        foreach (var (target, headSnap) in headsToRegister)
            headSnap.AutoSnapToTarget(target);
    }

    // ── Animation ─────────────────────────────────────────────────────────

    private IEnumerator AnimateToPosition(GameObject obj, Vector3 target)
    {
        SetKinematic(obj, true);

        Vector3 start = obj.transform.position;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            obj.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        obj.transform.position = target;
    }

    /// <summary>Move and turn an object to a target pose over animationDuration.</summary>
    private IEnumerator AnimateToPose(GameObject obj, Vector3 targetPos, Quaternion targetRot)
    {
        SetKinematic(obj, true);

        Vector3 startPos = obj.transform.position;
        Quaternion startRot = obj.transform.rotation;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            obj.transform.position = Vector3.Lerp(startPos, targetPos, t);
            obj.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        obj.transform.position = targetPos;
        obj.transform.rotation = targetRot;
    }

    // ── Position Helpers ──────────────────────────────────────────────────

    private Dictionary<string, int> targetWeightCount = new Dictionary<string, int>();
    private Dictionary<string, WeightSnap> lastAutoSnappedPerTarget = new Dictionary<string, WeightSnap>();

    private Vector3 GetWeightTargetPosition(string target)
    {
        if (!targetWeightCount.ContainsKey(target))
            targetWeightCount[target] = 0;

        int stackIndex = targetWeightCount[target];
        targetWeightCount[target]++;

        Vector3 basePosition;
        switch (target)
        {
            case "Hook_L":
                basePosition = hookL.transform.position;
                break;
            case "Hook_R":
                basePosition = hookR.transform.position;
                break;
            case "MovablePulley":
                basePosition = movablePulley.transform.position + Vector3.down * 0.1f;
                break;
            default:
                Debug.LogWarning($"[ExperimentConfigManager] Unknown target: {target}");
                return Vector3.zero;
        }

        return basePosition + Vector3.down * 0.05f * stackIndex;
    }

    // ── Object Finders ────────────────────────────────────────────────────

    private GameObject FindWeight(string weightName)
    {
        foreach (GameObject w in allWeights)
            if (w.name == weightName) return w;
        Debug.LogWarning($"[ExperimentConfigManager] Weight not found: {weightName}");
        return null;
    }

    // ── Show All Equipment ────────────────────────────────────────────────

    public void ShowAllEquipment()
    {
        if (resetManager != null)
            resetManager.ResetAll();

        fixedPulley.SetActive(true);
        movablePulley.SetActive(true);
        foreach (GameObject w in allWeights)
            w.SetActive(true);

        SetGrabbable(fixedPulley, true);
        SetGrabbable(movablePulley, true);
        SetGrabbable(hookL, true);
        SetGrabbable(hookR, true);
        foreach (GameObject w in allWeights)
            SetGrabbable(w, true);

        currentConfig = null;
        Debug.Log("[ExperimentConfigManager] All equipment shown");
    }

    // ── Reset To Current Config ───────────────────────────────────────────

    public void ResetToCurrentConfig()
    {
        targetWeightCount = new Dictionary<string, int>();

        if (resetManager != null)
            resetManager.ResetAll();

        if (currentConfig == null)
        {
            ShowAllEquipment();
            return;
        }

        foreach (Coroutine c in activeCoroutines)
            if (c != null) StopCoroutine(c);
        activeCoroutines.Clear();

        ApplyVisibility(currentConfig);

        if (currentConfig.useFixedPulley && hangers.Length > currentConfig.fixedPulleyHangerIndex)
            fixedPulley.transform.position = hangers[currentConfig.fixedPulleyHangerIndex].position;

        if (currentConfig.useMovablePulley)
        {
            movablePulley.SetActive(true);
            movablePulley.transform.position = fixedPulley.transform.position
                + currentConfig.movablePulleyOffset.ToVector3();
        }

        foreach (WeightAssignment wa in currentConfig.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight == null) continue;
            weight.transform.position = GetWeightTargetPosition(wa.target);
        }

        ApplyHighFidelity(currentConfig);

        Debug.Log("[ExperimentConfigManager] Reset to current config instantly");
    }

    // ── Component Helpers ─────────────────────────────────────────────────

    private void SetGrabbable(GameObject obj, bool enabled)
    {
        if (obj == null) return;
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null) grab.enabled = enabled;
    }

    private void SetKinematic(GameObject obj, bool kinematic)
    {
        if (obj == null) return;
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = kinematic;
    }

    // ── Play Button Entry Points ─────────────────────────────────────────

    public void StartSingleEquip(ExperimentConfig config, string aidType, System.Action onComplete)
    {
        StopCurrentDemo();
        currentDemoCoroutine = StartCoroutine(RunSingleEquip(config, aidType, onComplete));
    }

    public void StartDemoSequence(ExperimentConfig[] steps, string aidType, System.Action onComplete)
    {
        StopCurrentDemo();
        currentDemoCoroutine = StartCoroutine(RunDemoSequence(steps, aidType, onComplete));
    }

    /// <summary>True while a demo or auto-placement animation is running.</summary>
    public bool IsPlaying { get { return currentDemoCoroutine != null; } }
    public void StopCurrentDemo()
    {
        if (currentDemoCoroutine != null)
        {
            StopCoroutine(currentDemoCoroutine);
            currentDemoCoroutine = null;
        }
    }

    private IEnumerator RunSingleEquip(ExperimentConfig config, string aidType, System.Action onComplete)
    {
        if (resetManager != null)
        {
            resetManager.ResetAll();
            yield return null;
        }

        yield return StartCoroutine(ApplyLowFidelity(config, aidType));

        currentDemoCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator RunDemoSequence(ExperimentConfig[] steps, string aidType, System.Action onComplete)
    {
        if (steps == null || steps.Length == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (resetManager != null)
        {
            resetManager.ResetAll();
            yield return null;
        }

        yield return StartCoroutine(ForceDetachAllWeights());

        // Pulley placement/locking happens ONCE for the whole sequence — all
        // steps share the same pulley configuration, only weights change.
        yield return StartCoroutine(SetupPulleysForDemo(steps[0]));

        bool usesMovablePulley = steps[0].useMovablePulley;
        float neutralLoadY = 0f;
        float neutralForceY = 0f;
        if (usesMovablePulley)
        {
            neutralLoadY = movablePulley.transform.position.y;
            Transform freeEnd = pulleyPhysics != null ? pulleyPhysics.GetFreeEndHook() : null;
            neutralForceY = freeEnd != null ? freeEnd.position.y : 0f;
        }

        foreach (ExperimentConfig step in steps)
        {
            yield return StartCoroutine(PlayDemoStep(step, aidType));

            // After weights are retracted, snap the rope's Y-position back to
            // neutral (not the pulleys themselves) before the next step delivers.
            if (usesMovablePulley && pulleyPhysics != null)
                pulleyPhysics.ResetRopeGeometry(neutralLoadY, neutralForceY);
        }

        currentDemoCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator SetupPulleysForDemo(ExperimentConfig referenceStep)
    {
        fixedPulley.SetActive(referenceStep.useFixedPulley);
        movablePulley.SetActive(referenceStep.useMovablePulley);

        PulleySlot targetSlot = null;

        if (referenceStep.useFixedPulley && referenceStep.fixedPulleyHangerIndex < hangers.Length)
        {
            Vector3 hangerTarget = hangers[referenceStep.fixedPulleyHangerIndex].position;
            yield return StartCoroutine(AnimateToPosition(fixedPulley, hangerTarget));

            targetSlot = hangers[referenceStep.fixedPulleyHangerIndex].GetComponentInChildren<PulleySlot>();
            if (targetSlot != null)
                targetSlot.SnapPulley(fixedPulley);
        }

        if (referenceStep.useMovablePulley)
        {
            Vector3 movableTarget = fixedPulley.transform.position
                                  + referenceStep.movablePulleyOffset.ToVector3();
            yield return StartCoroutine(AnimateToPosition(movablePulley, movableTarget));

            MovablePulleyLock mpl = movablePulley.GetComponent<MovablePulleyLock>();
            if (mpl != null && targetSlot != null)
                mpl.AutoLockToFixedPulley(targetSlot);
        }
    }

    // ── Demo Sequence Playback (per-step) ────────────────────────────────

    private IEnumerator PlayDemoStep(ExperimentConfig step, string aidType)
    {
        targetWeightCount.Clear();

        yield return StartCoroutine(GroupAndDeliverWeights(step));
        yield return StartCoroutine(MaybePlayForceReveal(aidType));

        if (step.holdOnly)
        {
            yield return new WaitForSeconds(step.holdSeconds > 0 ? step.holdSeconds : 2f);
        }
        else
        {
            GameObject referenceWeight = FindWeight(step.weightAssignments[0].weightName);
            yield return StartCoroutine(WaitUntilSettled(referenceWeight));
        }

        yield return StartCoroutine(RetractDemoWeights(step));
    }

    private IEnumerator MaybePlayForceReveal(string aidType)
    {
        Debug.Log($"[AidDebug] MaybePlayForceReveal called, aidType={aidType}, conceptualAidManager null? {conceptualAidManager == null}");
        if (conceptualAidManager == null) yield break;
        if (string.IsNullOrEmpty(aidType) || aidType != "force") yield break;
        if (!conceptualAidManager.IsAidEnabled()) yield break;

        yield return StartCoroutine(conceptualAidManager.PlayForceReveal(aidType));
    }

    private IEnumerator WaitUntilSettled(GameObject referenceWeight)
    {
        if (referenceWeight == null)
        {
            yield return new WaitForSeconds(2f);
            yield break;
        }

        float safetyTimeout = 8f;
        float stableRequiredTime = 0.4f;
        float elapsed = 0f;
        float stableElapsed = 0f;
        Vector3 lastPos = referenceWeight.transform.position;

        while (elapsed < safetyTimeout)
        {
            yield return null;
            elapsed += Time.deltaTime;

            Vector3 currentPos = referenceWeight.transform.position;
            if ((currentPos - lastPos).sqrMagnitude < 0.0001f)
            {
                stableElapsed += Time.deltaTime;
                if (stableElapsed >= stableRequiredTime)
                    yield break;
            }
            else
            {
                stableElapsed = 0f;
            }

            lastPos = currentPos;
        }
    }

    private IEnumerator RetractDemoWeights(ExperimentConfig step)
    {
        List<Coroutine> moves = new List<Coroutine>();

        foreach (WeightAssignment wa in step.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight == null) continue;

            WeightSnap ws = weight.GetComponent<WeightSnap>();
            if (ws != null) ws.ForceDetach();

            Vector3 home = resetManager != null ? resetManager.GetInitialPosition(weight) : weight.transform.position;
            moves.Add(StartCoroutine(AnimateToPosition(weight, home)));
        }

        foreach (Coroutine c in moves)
            yield return c;
    }

    private IEnumerator ForceDetachAllWeights()
    {
        List<Coroutine> moves = new List<Coroutine>();

        foreach (GameObject w in allWeights)
        {
            WeightSnap ws = w.GetComponent<WeightSnap>();
            if (ws != null) ws.ForceDetach();

            if (resetManager != null)
            {
                Vector3 home = resetManager.GetInitialPosition(w);
                moves.Add(StartCoroutine(AnimateToPosition(w, home)));
            }
        }

        foreach (Coroutine c in moves)
            yield return c;
    }
}