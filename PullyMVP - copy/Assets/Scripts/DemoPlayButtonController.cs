using UnityEngine;
using TMPro;

/// <summary>
/// Drives the Play button.
///
/// The button serves two purposes, driven by two separate switches on
/// FidelityManager (group B can turn either on without the other):
///
///   auto setup ON  -> play the equipment demo / placement first
///   aid ON         -> afterwards, replay the conceptual aid
///
///   manual, no aid  (Level 2)  -> button hidden
///   manual + aid    (Level 1)  -> replay the conceptual aid only
///   auto + aid      (Level 0)  -> equipment, then aid replay
///   auto, no aid    (group B)  -> equipment only; no aid replay afterwards
///
/// With manual setup the button never moves equipment: object placement is
/// the student's own at high fidelity.
/// </summary>
public class DemoPlayButtonController : MonoBehaviour
{
    [Header("UI")]
    public GameObject playButtonRoot;
    public TouchButton playButton;
    [Tooltip("Label on the button. Its text changes with what the press will do.")]
    public TMP_Text playButtonLabel;
    [Tooltip("Shown when the press will run the equipment / weight-hanging demo.")]
    public string equipmentLabel = "Set Up Weights";
    [Tooltip("Shown when the press will replay the force diagram. Must say so: " +
             "one button serving two jobs is unusable if the label does not " +
             "distinguish them.")]
    public string aidReplayLabel = "Show Forces Again";

    [Header("References")]
    public FidelityManager fidelityManager;
    public QuizManager quizManager;
    public ExperimentConfigManager experimentConfigManager;
    public ConceptualAidManager conceptualAidManager;

    private bool isPlaying = false;
    private bool hasPlayedStaticEquip = false;
    private bool hasPlayedDemoSequence = false;
    private DifficultySetting lastSeenDifficulty = null;

    void Start()
    {
        if (playButton != null)
            playButton.onTouched.AddListener(OnPlayClicked);
        if (playButtonRoot != null)
            playButtonRoot.SetActive(false);
    }

    void Update()
    {
        // Detect a question/difficulty change on our own, so a missing
        // Inspector wiring can't leave the button permanently hidden.
        DifficultySetting current = quizManager != null
            ? quizManager.GetCurrentDifficultySettingPublic()
            : null;

        if (current != lastSeenDifficulty)
        {
            lastSeenDifficulty = current;
            isPlaying = false;
            hasPlayedStaticEquip = false;
            hasPlayedDemoSequence = false;
            if (experimentConfigManager != null)
                experimentConfigManager.StopCurrentDemo();
        }

        if (playButtonRoot == null) return;

        if (isPlaying)
        {
            if (playButtonRoot.activeSelf)
                playButtonRoot.SetActive(false);
            return;
        }

        bool shouldShow = ShouldShowButton();
        if (playButtonRoot.activeSelf != shouldShow)
            playButtonRoot.SetActive(shouldShow);

        UpdateButtonLabel(current);
    }

    /// <summary>
    /// The same button serves two purposes, so it must say which one. Without
    /// this the participant cannot tell "hang the weights" from "show the
    /// forces again".
    /// </summary>
    private void UpdateButtonLabel(DifficultySetting d)
    {
        if (playButtonLabel == null || d == null || fidelityManager == null) return;

        string wanted = CanRunEquipment(d) ? equipmentLabel : aidReplayLabel;

        if (playButtonLabel.text != wanted)
            playButtonLabel.text = wanted;
    }

    private bool AutoSetupOn()
    {
        return fidelityManager != null && fidelityManager.IsLowFidelity();
    }

    private bool AidOn()
    {
        return fidelityManager != null && fidelityManager.IsAidOn();
    }

    private bool ShouldShowButton()
    {
        if (fidelityManager == null || quizManager == null) return false;

        DifficultySetting d = quizManager.GetCurrentDifficultySettingPublic();
        if (d == null) return false;

        if (CanRunEquipment(d)) return true;
        if (CanReplayAid()) return true;

        return false;
    }

    private bool CanRunEquipment(DifficultySetting d)
    {
        if (!AutoSetupOn()) return false;

        // Once the demo has run, the button's job changes: pressing it again
        // should replay the force diagram, not re-hang the weights. Without
        // this there is no way to review the forces at low fidelity.
        bool hasDemo = d.demoSequences != null && d.demoSequences.Length > 0;
        if (hasDemo) return !hasPlayedDemoSequence;

        bool hasStatic = d.experimentConfigs != null && d.experimentConfigs.Length > 0;
        return hasStatic && !hasPlayedStaticEquip;
    }

    private bool CanReplayAid()
    {
        // No aid -> no aid replay, also when auto setup is on.
        if (!AidOn()) return false;
        return conceptualAidManager != null && conceptualAidManager.CanReplay();
    }

    private void OnPlayClicked()
    {
        if (isPlaying) return;

        DifficultySetting d = quizManager != null
            ? quizManager.GetCurrentDifficultySettingPublic()
            : null;
        if (d == null) return;

        // Equipment first: at low fidelity the demo ends with the system at
        // rest, which is exactly what lets the force aid trigger itself.
        if (CanRunEquipment(d) && experimentConfigManager != null)
        {
            SessionLogger.Log("PlayPressed", "Participant", "equipment");
            isPlaying = true;
            playButtonRoot.SetActive(false);

            bool hasDemo = d.demoSequences != null && d.demoSequences.Length > 0;

            if (hasDemo)
            {
                experimentConfigManager.StartDemoSequence(
                    d.demoSequences, d.aidType, OnPlaybackComplete);
            }
            else
            {
                experimentConfigManager.StartSingleEquip(
                    d.experimentConfigs[0], d.aidType, OnStaticPlaybackComplete);
            }
            return;
        }

        // Otherwise the press means "show me that again".
        if (!CanReplayAid()) return;

        SessionLogger.Log("PlayPressed", "Participant", "aidReplay");
        conceptualAidManager.OnReplayRequested();

        // A demo retracts the weights when it ends, so replaying the force
        // diagram on an empty rig would show forces with no visible source.
        // Re-place the question's own configuration first and let the aid
        // sample the real weights, so the diagram and the apparatus agree.
        ExperimentConfig replayConfig = ResolveReplayConfig(d);
        if (AutoSetupOn()
            && experimentConfigManager != null
            && replayConfig != null)
        {
            isPlaying = true;
            playButtonRoot.SetActive(false);
            experimentConfigManager.StartSingleEquip(
                replayConfig, d.aidType, OnReplayEquipComplete);
        }
    }

    /// <summary>
    /// The configuration a replay should restore. The static setup is the
    /// question's finished state; if a question only has demo steps, its last
    /// step is that finished state.
    /// </summary>
    private ExperimentConfig ResolveReplayConfig(DifficultySetting d)
    {
        if (d == null) return null;

        if (d.experimentConfigs != null && d.experimentConfigs.Length > 0)
            return d.experimentConfigs[0];

        if (d.demoSequences != null && d.demoSequences.Length > 0)
            return d.demoSequences[d.demoSequences.Length - 1];

        return null;
    }

    private void OnReplayEquipComplete()
    {
        // Deliberately does NOT set hasPlayedStaticEquip: the button should
        // come back as "Show Forces Again", not disappear for good.
        isPlaying = false;
    }

    private void OnPlaybackComplete()
    {
        // The demo is done, but the force aid's comparison stage may still be
        // running. The button stays hidden until ConceptualAidManager reports
        // it has settled - CanReplay() covers that - so the student never sees
        // a replay offer for an animation still in progress.
        hasPlayedDemoSequence = true;
        isPlaying = false;
    }

    private void OnStaticPlaybackComplete()
    {
        hasPlayedStaticEquip = true;
        isPlaying = false;
    }

    // Kept for compatibility - harmless if QuizManager also calls it.
    public void ForceStop()
    {
        isPlaying = false;
        hasPlayedStaticEquip = false;
        if (experimentConfigManager != null)
            experimentConfigManager.StopCurrentDemo();
    }
}