using UnityEngine;

/// <summary>
/// Experimenter control panel. Lives on AdaptiveSystem and is operated from
/// the Inspector during Play mode. Nothing is drawn in the Game view or the
/// headset.
///
/// What the support controls do depends on FidelityManager.group:
///   A  no controls; fixed at Level 2
///   B  Aid ON/OFF and Auto setup ON/OFF, pressed when the participant asks
///      out loud (logged as LearnerRequest); L2/L1/L0 as shortcuts
///   C  L2/L1/L0 override if the classifier fails (logged as WizardOverride),
///      plus Return to CL control
///
/// The buttons are drawn by Editor/WizardControlPanelEditor.cs.
/// Every action is also available from the component's menu as a fallback.
/// </summary>
public class WizardControlPanel : MonoBehaviour
{
    [Header("References")]
    public QuizManager quizManager;
    public FidelityManager fidelityManager;
    public SFXManager sfxManager;

    private void Reset()
    {
        // Auto-fill references when the component is first added.
        if (quizManager == null) quizManager = FindFirstObjectByType<QuizManager>();
        if (fidelityManager == null) fidelityManager = GetComponent<FidelityManager>();
        if (sfxManager == null) sfxManager = GetComponent<SFXManager>();
    }

    private void Awake()
    {
        if (sfxManager == null) sfxManager = GetComponent<SFXManager>();
    }

    // -- Baseline calibration music --------------------------------------------

    public void StartBaselineMusic()
    {
        if (!Application.isPlaying || sfxManager == null) return;
        sfxManager.StartBaselineMusic();
        SessionLogger.Log("BaselineMusicStart", "Wizard", "");
    }

    public void StopBaselineMusic()
    {
        if (!Application.isPlaying || sfxManager == null) return;
        sfxManager.StopBaselineMusic();
        SessionLogger.Log("BaselineMusicStop", "Wizard", "");
    }

    public bool IsBaselineMusicPlaying()
    {
        return sfxManager != null && sfxManager.IsMusicPlaying;
    }

    [ContextMenu("Start baseline music")] private void MenuMusicStart() { StartBaselineMusic(); }
    [ContextMenu("Stop baseline music (fade out)")] private void MenuMusicStop() { StopBaselineMusic(); }

    // -- Question navigation -----------------------------------------------------

    public void JumpTo(int questionIndex, bool hard)
    {
        if (!Application.isPlaying || quizManager == null) return;
        quizManager.JumpTo(questionIndex, hard);
    }

    public void Next()
    {
        if (!Application.isPlaying || quizManager == null || !quizManager.IsLoaded()) return;
        quizManager.NextQuestion();
    }

    public void Prev()
    {
        if (!Application.isPlaying || quizManager == null || !quizManager.IsLoaded()) return;
        quizManager.PrevQuestion();
    }

    // -- Support controls ----------------------------------------------------------

    /// <summary>Group B: learner request shortcut. Group C: manual override.</summary>
    public void SetLevel(int level)
    {
        if (!Application.isPlaying || fidelityManager == null) return;
        fidelityManager.ForceLevel(level);
    }

    /// <summary>Group B only.</summary>
    public void SetAid(bool on)
    {
        if (!Application.isPlaying || fidelityManager == null) return;
        fidelityManager.SetAid(on);
    }

    /// <summary>Group B only.</summary>
    public void SetAutoSetup(bool on)
    {
        if (!Application.isPlaying || fidelityManager == null) return;
        fidelityManager.SetAutoSetup(on);
    }

    /// <summary>Group C only.</summary>
    public void ReturnToCLControl()
    {
        if (!Application.isPlaying || fidelityManager == null) return;
        fidelityManager.ReturnToCLControl();
    }

    [ContextMenu("Level 2 (manual setup, no aid)")] private void MenuLevel2() { SetLevel(2); }
    [ContextMenu("Level 1 (manual setup + aid)")] private void MenuLevel1() { SetLevel(1); }
    [ContextMenu("Level 0 (auto setup + aid)")] private void MenuLevel0() { SetLevel(0); }
    [ContextMenu("B: Aid ON")] private void MenuAidOn() { SetAid(true); }
    [ContextMenu("B: Aid OFF")] private void MenuAidOff() { SetAid(false); }
    [ContextMenu("B: Auto setup ON")] private void MenuAutoOn() { SetAutoSetup(true); }
    [ContextMenu("B: Auto setup OFF")] private void MenuAutoOff() { SetAutoSetup(false); }
    [ContextMenu("Next question")] private void MenuNext() { Next(); }
    [ContextMenu("Previous question")] private void MenuPrev() { Prev(); }

    // -- Status (read by the Inspector) ---------------------------------------------

    public string DescribeQuestion()
    {
        if (quizManager == null) return "QuizManager not assigned";
        if (!quizManager.IsLoaded()) return "Questions not loaded yet";
        int i = quizManager.GetCurrentIndex();
        bool hard = quizManager.IsCurrentHard();
        return quizManager.GetQuestionLabel(i, hard) + "  (" + (hard ? "hard" : "normal") +
               ")   flow: " + quizManager.GetFlowPhaseName();
    }

    public static string DescribeCode(int code)
    {
        switch (code)
        {
            case 2: return "L2  manual setup, no aid";
            case 1: return "L1  manual setup + aid";
            case 0: return "L0  auto setup + aid";
            case FidelityManager.AutoNoAidCode: return "auto setup, no aid";
            default: return "?";
        }
    }

    public string DescribeLevel()
    {
        if (fidelityManager == null) return "FidelityManager not assigned";
        FidelityManager fm = fidelityManager;

        string s = "Group " + fm.GetGroupLabel() + "\n" +
                   "Displayed level:  " + FidelityManager.DescribeLevel(fm.GetCurrentLevel()) +
                   (fm.manualMode ? "   [MANUAL OVERRIDE]" : "") + "\n" +
                   "Actual level:     " + FidelityManager.DescribeLevel(fm.GetFidelityLevel()) +
                   "   CL = " + fm.currentCLScore.ToString("F0") +
                   (fm.GetFidelityLevel() == FidelityManager.MaxLevel ? "   (hard-question rule possible)" : "");
        if (fm.HasPendingLevelChange())
            s += "\nChange queued - applies when the participant lets go";
        return s;
    }
}
