using UnityEngine;

/// <summary>
/// Wizard-of-Oz control panel. Lives on AdaptiveSystem and is operated from
/// the Inspector during Play mode, the same way the CL slider on
/// FidelityManager is. Nothing is drawn in the Game view or the headset.
///
/// The buttons themselves are drawn by Editor/WizardControlPanelEditor.cs.
/// Every action is also available from the component's ⋮ menu as a fallback.
/// </summary>
public class WizardControlPanel : MonoBehaviour
{
    [Header("References")]
    public QuizManager quizManager;
    public FidelityManager fidelityManager;

    private void Reset()
    {
        // Auto-fill references when the component is first added.
        if (quizManager == null) quizManager = FindFirstObjectByType<QuizManager>();
        if (fidelityManager == null) fidelityManager = GetComponent<FidelityManager>();
    }

    // ── Question navigation ───────────────────────────────────────────────

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

    // ── Fidelity level ────────────────────────────────────────────────────

    public void SetLevel(int level)
    {
        if (!Application.isPlaying || fidelityManager == null) return;
        fidelityManager.ForceLevel(level);
    }

    public void ReturnToCLControl()
    {
        if (!Application.isPlaying || fidelityManager == null) return;
        fidelityManager.ReturnToCLControl();
    }

    [ContextMenu("Level 2 (high fidelity, no aid)")] private void MenuLevel2() { SetLevel(2); }
    [ContextMenu("Level 1 (high fidelity + aid)")] private void MenuLevel1() { SetLevel(1); }
    [ContextMenu("Level 0 (low fidelity + aid)")] private void MenuLevel0() { SetLevel(0); }
    [ContextMenu("Next question")] private void MenuNext() { Next(); }
    [ContextMenu("Previous question")] private void MenuPrev() { Prev(); }

    // ── Status (read by the Inspector) ────────────────────────────────────

    public string DescribeQuestion()
    {
        if (quizManager == null) return "QuizManager not assigned";
        if (!quizManager.IsLoaded()) return "Questions not loaded yet";
        int i = quizManager.GetCurrentIndex();
        bool hard = quizManager.IsCurrentHard();
        return quizManager.GetQuestionLabel(i, hard) + "  (" + (hard ? "hard" : "normal") +
               ")   flow: " + quizManager.GetFlowPhaseName();
    }

    public string DescribeLevel()
    {
        if (fidelityManager == null) return "FidelityManager not assigned";
        int level = fidelityManager.GetCurrentLevel();
        string name = level == 2 ? "high fidelity, no aid"
                    : level == 1 ? "high fidelity + aid"
                    : "low fidelity + aid";
        string s = "Level " + level + "  (" + name + ")   " +
                   (fidelityManager.manualMode
                       ? "MANUAL"
                       : "CL = " + fidelityManager.currentCLScore.ToString("F0"));
        if (fidelityManager.HasPendingLevelChange())
            s += "\nChange queued - applies when the participant lets go";
        return s;
    }
}