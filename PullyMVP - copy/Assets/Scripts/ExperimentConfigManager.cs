using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// ���� Data Classes ����������������������������������������������������������������������������������������������������������������������������

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
    public int fixedPulleyHangerIndex;          // 0-4, used in low fidelity
    public Vector3Data movablePulleyOffset;     // Offset from fixed pulley
    public WeightAssignment[] weightAssignments;
}

// ���� ExperimentConfigManager ������������������������������������������������������������������������������������������������������

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

    // Current active config
    private ExperimentConfig currentConfig;

    // Active coroutines tracker
    private List<Coroutine> activeCoroutines = new List<Coroutine>();

    // ���� Fidelity Level Change ��������������������������������������������������������������������������

    // Called by FidelityManager.onFidelityLevelChanged event
    public void OnFidelityLevelChanged(int newLevel)
    {
        if (currentConfig == null) return;
        ApplyConfig(currentConfig);
    }

    // ���� Main Entry Point ������������������������������������������������������������������������������������

    public void ApplyConfig(ExperimentConfig config)
    {
        currentConfig = config;

        // Stop any running animations
        foreach (Coroutine c in activeCoroutines)
            if (c != null) StopCoroutine(c);
        activeCoroutines.Clear();

        // Step 1: Show/hide equipment
        ApplyVisibility(config);

        // Step 2: Apply interaction based on fidelity level
        int level = fidelityManager != null ? fidelityManager.GetCurrentLevel() : 3;
        bool isHighFidelity = (level == 2 || level == 3);

        if (isHighFidelity)
            ApplyHighFidelity(config);
        else
            activeCoroutines.Add(StartCoroutine(ApplyLowFidelity(config)));
    }

    // ���� Step 1: Visibility ��������������������������������������������������������������������������������

    private void ApplyVisibility(ExperimentConfig config)
    {
        // Show/hide pulleys
        fixedPulley.SetActive(config.useFixedPulley);
        movablePulley.SetActive(config.useMovablePulley);

        // Hide all weights first
        foreach (GameObject w in allWeights)
            w.SetActive(false);

        // Show only weights needed in this config
        foreach (WeightAssignment wa in config.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight != null)
                weight.SetActive(true);
        }
    }

    // ���� Step 2a: High Fidelity ������������������������������������������������������������������������

    private void ApplyHighFidelity(ExperimentConfig config)
    {
        // Enable all grabbable equipment, no position restrictions
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

    // ���� Step 2b: Low Fidelity ��������������������������������������������������������������������������

    private IEnumerator ApplyLowFidelity(ExperimentConfig config)
    {
        // Disable all grabbing during animation
        SetGrabbable(fixedPulley, false);
        SetGrabbable(movablePulley, false);
        SetGrabbable(hookL, false);
        SetGrabbable(hookR, false);

        foreach (WeightAssignment wa in config.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight != null) SetGrabbable(weight, false);
        }

        // Animate fixed pulley to target hanger
        if (config.useFixedPulley && config.fixedPulleyHangerIndex < hangers.Length)
        {
            Vector3 hangerTarget = hangers[config.fixedPulleyHangerIndex].position;
            yield return StartCoroutine(AnimateToPosition(fixedPulley, hangerTarget));
        }

        // Animate movable pulley relative to fixed pulley
        if (config.useMovablePulley)
        {
            Vector3 movableTarget = fixedPulley.transform.position
                                  + config.movablePulleyOffset.ToVector3();
            yield return StartCoroutine(AnimateToPosition(movablePulley, movableTarget));
        }

        // Animate weights to their targets
        foreach (WeightAssignment wa in config.weightAssignments)
        {
            GameObject weight = FindWeight(wa.weightName);
            if (weight == null) continue;

            Vector3 target = GetWeightTargetPosition(wa.target);
            yield return StartCoroutine(AnimateToPosition(weight, target));
        }

        Debug.Log("[ExperimentConfigManager] Low fidelity: animation complete");
    }

    // ���� Animation ��������������������������������������������������������������������������������������������������

    private IEnumerator AnimateToPosition(GameObject obj, Vector3 target)
    {
        // Disable physics during animation
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

    // ���� Position Helpers ������������������������������������������������������������������������������������

    private Vector3 GetWeightTargetPosition(string target)
    {
        switch (target)
        {
            case "Hook_L":
                return hookL.transform.position;
            case "Hook_R":
                return hookR.transform.position;
            case "MovablePulley":
                return movablePulley.transform.position
                     + Vector3.down * 0.1f;
            default:
                Debug.LogWarning($"[ExperimentConfigManager] Unknown target: {target}");
                return Vector3.zero;
        }
    }

    // ���� Object Finders ����������������������������������������������������������������������������������������

    private GameObject FindWeight(string weightName)
    {
        foreach (GameObject w in allWeights)
            if (w.name == weightName) return w;
        Debug.LogWarning($"[ExperimentConfigManager] Weight not found: {weightName}");
        return null;
    }

    // ���� Component Helpers ����������������������������������������������������������������������������������

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
}