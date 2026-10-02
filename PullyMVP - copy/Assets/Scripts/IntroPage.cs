using System.Collections;
using UnityEngine;

// Shows an instruction page before question 1-1.
// During the intro, all equipment is in its editor start position (state C).
// Attach to QuizPaper (always active), NOT to IntroPanel.
public class IntroPage : MonoBehaviour
{
    [Header("References")]
    public GameObject introPanel;   // the duplicated panel with instruction text + Next
    public GameObject paperPanel;   // the original quiz panel
    public QuizManager quizManager;

    [Tooltip("Short gap so the same trigger press cannot also hit the quiz's Confirm button.")]
    public float switchDelay = 0.4f;

    private bool quizLoaded = false;
    private bool introDone = false;

    IEnumerator Start()
    {
        introPanel.SetActive(true);

        // QuizManager needs PaperPanel active while it draws question 1.
        // Wait until the JSON is loaded and the question is drawn.
        while (quizManager.GetCurrentDifficultySettingPublic() == null)
            yield return null;

        quizLoaded = true;
        if (introDone) yield break;

        paperPanel.SetActive(false);

        // Replace the 1-1 setup with all equipment in its start position.
        if (quizManager.experimentConfigManager != null)
            quizManager.experimentConfigManager.ShowAllEquipment();
    }

    // Called by the intro Next button's TouchButton.
    public void OnNextPressed()
    {
        if (introDone || !quizLoaded) return;
        introDone = true;
        StartCoroutine(SwitchToQuiz());
    }

    private IEnumerator SwitchToQuiz()
    {
        introPanel.SetActive(false);

        ExperimentConfigManager ecm = quizManager.experimentConfigManager;
        if (ecm != null)
        {
            // Clear anything the participant touched, then load the 1-1 setup.
            if (ecm.resetManager != null) ecm.resetManager.ResetAll();

            DifficultySetting d = quizManager.GetCurrentDifficultySettingPublic();
            if (d.experimentConfigs != null && d.experimentConfigs.Length > 0)
                ecm.ApplyConfig(d.experimentConfigs[0]);
        }

        yield return new WaitForSeconds(switchDelay);
        paperPanel.SetActive(true);
    }
}