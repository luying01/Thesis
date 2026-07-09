using UnityEngine;
using UnityEngine.Events;

public class FidelityManager : MonoBehaviour
{
    [Header("Configuration")]
    public FidelityConfig config;

    [Header("CL Input (0-100)")]
    [Range(0f, 100f)]
    public float currentCLScore = 0f;

    [Header("Current Level (Read Only)")]
    [SerializeField] private int currentLevel = 3;

    [Header("Events")]
    public UnityEvent<int> onFidelityLevelChanged;

    private float _lastCLScore = -1f;

    private void Update()
    {
        // Only recalculate if CL score changed
        if (Mathf.Approximately(_lastCLScore, currentCLScore)) return;
        _lastCLScore = currentCLScore;
        EvaluateLevel();
    }

    private void EvaluateLevel()
    {
        int newLevel = CalculateLevel(currentCLScore, currentLevel);
        if (newLevel == currentLevel) return;

        currentLevel = newLevel;
        onFidelityLevelChanged.Invoke(currentLevel);
        Debug.Log($"[FidelityManager] Level changed to: {currentLevel} (CL={currentCLScore})");
    }

    private int CalculateLevel(float cl, int current)
    {
        float h = config.hysteresis;

        // CL rising: switch to lower fidelity
        if (current == 3 && cl >= config.toLevel2) return 2;
        if (current == 2 && cl >= config.toLevel1) return 1;
        if (current == 1 && cl >= config.toLevel0) return 0;

        // CL falling: switch to higher fidelity
        if (current == 0 && cl < config.toLevel0 - h) return 1;
        if (current == 1 && cl < config.toLevel1 - h) return 2;
        if (current == 2 && cl < config.toLevel2 - h) return 3;

        return current;
    }

    // Teammate calls this to set CL score from their module
    public void SetCLScore(float score)
    {
        currentCLScore = Mathf.Clamp(score, 0f, 100f);
    }

    public int GetCurrentLevel() => currentLevel;
}
