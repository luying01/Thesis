using UnityEngine;

public class DemoPlayButtonController : MonoBehaviour
{
    [Header("UI")]
    public GameObject playButtonRoot;
    public TouchButton playButton;

    [Header("References")]
    public FidelityManager fidelityManager;
    public QuizManager quizManager;
    public ExperimentConfigManager experimentConfigManager;

    [Header("Settings")]
    public int[] triggerLevels = new int[] { 0, 1 };

    private bool isPlaying = false;
    private bool hasPlayedStaticEquip = false;
    private DifficultySetting lastSeenDifficulty = null;

    void Start()
    {
        playButton.onTouched.AddListener(OnPlayClicked);
        playButtonRoot.SetActive(false);
    }

    void Update()
    {
        // Detect a question/difficulty change on our own ¡ª this no longer
        // relies on QuizManager remembering to call ForceStop(), so a missing
        // Inspector wiring can't leave the button permanently hidden.
        DifficultySetting current = quizManager != null ? quizManager.GetCurrentDifficultySettingPublic() : null;
        if (current != lastSeenDifficulty)
        {
            lastSeenDifficulty = current;
            isPlaying = false;
            hasPlayedStaticEquip = false;
            if (experimentConfigManager != null)
                experimentConfigManager.StopCurrentDemo();
        }

        if (isPlaying)
        {
            if (playButtonRoot.activeSelf)
                playButtonRoot.SetActive(false);
            return;
        }

        bool shouldShow = ShouldShowButton();
        if (playButtonRoot.activeSelf != shouldShow)
            playButtonRoot.SetActive(shouldShow);
    }

    private bool ShouldShowButton()
    {
        if (fidelityManager == null || quizManager == null) return false;

        int level = fidelityManager.GetCurrentLevel();
        if (System.Array.IndexOf(triggerLevels, level) < 0) return false;

        DifficultySetting d = quizManager.GetCurrentDifficultySettingPublic();
        if (d == null) return false;

        bool hasDemo = d.demoSequences != null && d.demoSequences.Length > 0;
        bool hasStatic = d.experimentConfigs != null && d.experimentConfigs.Length > 0;

        if (!hasDemo && !hasStatic) return false;
        if (!hasDemo && hasStatic && hasPlayedStaticEquip) return false;

        return true;
    }

    private void OnPlayClicked()
    {
        if (isPlaying) return;

        isPlaying = true;
        playButtonRoot.SetActive(false);

        DifficultySetting d = quizManager.GetCurrentDifficultySettingPublic();
        if (d == null || experimentConfigManager == null)
        {
            isPlaying = false;
            return;
        }

        bool hasDemo = d.demoSequences != null && d.demoSequences.Length > 0;

        if (hasDemo)
        {
            experimentConfigManager.StartDemoSequence(d.demoSequences, d.aidType, OnPlaybackComplete);
        }
        else if (d.experimentConfigs != null && d.experimentConfigs.Length > 0)
        {
            experimentConfigManager.StartSingleEquip(d.experimentConfigs[0], d.aidType, OnStaticPlaybackComplete);
        }
        else
        {
            isPlaying = false;
        }
    }

    private void OnPlaybackComplete()
    {
        isPlaying = false;
    }

    private void OnStaticPlaybackComplete()
    {
        hasPlayedStaticEquip = true;
        isPlaying = false;
    }

    // Kept for compatibility ¡ª no longer required for correctness, but
    // harmless if QuizManager also calls it.
    public void ForceStop()
    {
        isPlaying = false;
        hasPlayedStaticEquip = false;
        if (experimentConfigManager != null)
            experimentConfigManager.StopCurrentDemo();
    }
}