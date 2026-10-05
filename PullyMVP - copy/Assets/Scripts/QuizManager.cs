using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;

// -- Data Classes ----------------------------------------

[System.Serializable]
public class DifficultySetting
{
    public string questionText;
    public string imagePath;
    public string[] options;
    public int[] correctAnswer;
    public ExperimentConfig[] experimentConfigs;
    public ExperimentConfig[] demoSequences;
    public string aidType;
}

[System.Serializable]
public class Difficulties
{
    public DifficultySetting normal;
    public DifficultySetting hard;
}

[System.Serializable]
public class Question
{
    public int id;
    public Difficulties difficulties;
}

[System.Serializable]
public class QuestionList
{
    public Question[] questions;
}

public class QuizManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI questionText;
    public Button[] optionButtons;
    public Image questionImage;
    public Button prevButton;
    public Button nextButton;
    public TextMeshProUGUI nextButtonText;
    [Header("Input")]
    [Tooltip("Ignore repeat navigation input inside this window, to absorb " +
         "the double-fire from the UI and ray input paths overlapping.")]
    public float navInputCooldown = 0.35f;
    private float _lastNavInputTime = -1f;
    public TextMeshProUGUI feedbackText;

    [Header("Config Buttons")]
    public GameObject configButtonPrefab;
    public Transform configButtonContainer;

    [Header("References")]
    public ExperimentConfigManager experimentConfigManager;
    public FidelityManager fidelityManager;
    public DemoPlayButtonController demoPlayButtonController;
    public ConceptualAidManager conceptualAidManager;

    [Header("Selection Indicator")]
    public GameObject selectionRing;

    [Header("Difficulty Upgrade Timing")]
    public float upgradeDelaySeconds = 1.2f;

    // -- Internal State ----------------------------------------

    private QuestionList quizData;
    private int currentIndex = 0;
    private string currentDifficulty = "normal";
    private List<int> selectedAnswers = new List<int>();
    private List<int>[] studentAnswers;
    private List<GameObject> spawnedConfigButtons = new List<GameObject>();
    private ExperimentConfig[] currentExperimentConfigs;

    private bool awaitingConfirm = true;

    // Tracks whether the student ever answered wrong on THIS question's normal
    // version. Needed because condition A requires answering correctly on the
    // first attempt, not just eventually.
    private bool[] normalHadWrongAttempt;

    // -- Difficulty Flow State ----------------------------------------
    private enum FlowPhase { NormalRound, ImmediateHardInterrupt, HardBackfill }
    private FlowPhase flowPhase = FlowPhase.NormalRound;

    private bool[] hardPending;
    private bool[] hardShown;
    private bool quizFinished = false;

    private List<int> backfillQueue = new List<int>();
    private int backfillPointer = -1;

    private bool showingThankYou = false;

    // Handle for the delayed normal->hard switch, so a Wizard-of-Oz jump can
    // cancel it. Otherwise a jump made during the 1.2 s delay would be
    // overridden a moment later by the pending switch.
    private Coroutine immediateHardCoroutine;

    // -- Unity Lifecycle ----------------------------------------

    void Start()
    {
        prevButton.onClick.AddListener(PrevQuestion);
        nextButton.onClick.AddListener(OnConfirmOrNext);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => SelectAnswer(index));
        }

        StartCoroutine(LoadQuestionsCoroutine());
    }

    // -- Load ----------------------------------------

    IEnumerator LoadQuestionsCoroutine()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "questions.json");
        string url = path;
        if (!url.Contains("://"))
            url = "file://" + url;

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("[QuizManager] Failed to load questions.json: " + request.error + " | URL: " + url);
                yield break;
            }

            string json = request.downloadHandler.text;

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[QuizManager] questions.json is empty at: " + url);
                yield break;
            }

            quizData = JsonUtility.FromJson<QuestionList>(json);

            if (quizData == null || quizData.questions == null || quizData.questions.Length == 0)
            {
                Debug.LogError("[QuizManager] questions.json parsed but contains no questions.");
                yield break;
            }

            int count = quizData.questions.Length;
            studentAnswers = new List<int>[count];
            hardPending = new bool[count];
            hardShown = new bool[count];
            normalHadWrongAttempt = new bool[count];
            for (int i = 0; i < count; i++)
            {
                studentAnswers[i] = new List<int>();
                hardPending[i] = false;
                hardShown[i] = false;
                normalHadWrongAttempt[i] = false;
            }

            flowPhase = FlowPhase.NormalRound;
            quizFinished = false;
            showingThankYou = false;
            backfillQueue.Clear();
            backfillPointer = -1;

            Debug.Log($"[QuizManager] Loaded {count} questions successfully.");

            DisplayQuestion(currentIndex);
            SetConfirmMode();
        }
    }

    // -- Helpers ----------------------------------------

    private DifficultySetting GetCurrentDifficultySetting()
    {
        Question q = quizData.questions[currentIndex];
        return currentDifficulty == "hard" ? q.difficulties.hard : q.difficulties.normal;
    }

    // -- Display ----------------------------------------

    void DisplayQuestion(int index)
    {
        if (demoPlayButtonController != null) demoPlayButtonController.ForceStop();
        if (experimentConfigManager != null) experimentConfigManager.StopCurrentDemo();
        DifficultySetting d = GetCurrentDifficultySetting();
        if (conceptualAidManager != null) conceptualAidManager.SetCurrentAidType(d.aidType);

        questionText.text = d.imagePath + ". " + d.questionText;

        string[] labels = { "A", "B", "C", "D" };
        for (int i = 0; i < optionButtons.Length; i++)
        {
            TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            btnText.text = labels[i] + ". " + d.options[i];
        }

        if (!string.IsNullOrEmpty(d.imagePath))
        {
            questionImage.gameObject.SetActive(true);
            StartCoroutine(LoadImage(d.imagePath));
        }
        else
        {
            questionImage.gameObject.SetActive(false);
        }

        selectedAnswers = new List<int>(studentAnswers[index]);
        UpdateSelectionDisplay();

        currentExperimentConfigs = d.experimentConfigs;
        SpawnConfigButtons(currentExperimentConfigs);

        if (flowPhase == FlowPhase.HardBackfill || flowPhase == FlowPhase.ImmediateHardInterrupt)
            prevButton.interactable = true;
        else
            prevButton.interactable = (currentIndex > 0);

        if (currentExperimentConfigs != null && currentExperimentConfigs.Length > 0)
        {
            if (experimentConfigManager != null)
                experimentConfigManager.ApplyConfig(currentExperimentConfigs[0]);
        }
        else
        {
            if (experimentConfigManager != null)
                experimentConfigManager.ShowAllEquipment();
        }

        SetConfirmMode();
        ClearFeedback();
    }

    // -- Confirm / Next / Finish ----------------------------------------

    public void OnConfirmOrNext()
    {
        // Two input paths reach this button: QuizManager registers
        // Button.onClick, and QuizRaySelector calls TouchButton.TriggerButton
        // on the same object. TouchButton's own cooldown does not cover the
        // UI path, so one trigger press advanced two questions.
        if (Time.time - _lastNavInputTime < navInputCooldown) return;
        _lastNavInputTime = Time.time;
        if (quizFinished)
        {
            OnFinish();
            return;
        }

        if (awaitingConfirm)
            ConfirmAnswer();
        else
            NextQuestion();
    }

    private void ConfirmAnswer()
    {
        if (selectedAnswers.Count == 0)
        {
            ShowFeedback("Please select an answer first", Color.yellow);
            return;
        }

        DifficultySetting d = GetCurrentDifficultySetting();
        bool correct = IsAnswerCorrect(selectedAnswers, d.correctAnswer);

        if (correct)
        {
            bool triggerImmediateHard = false;

            if (currentDifficulty == "normal")
            {
                hardPending[currentIndex] = true;

                // Condition A: first-attempt correct AND CL was at the lowest band
                // (Level 2, the highest fidelity level) at the time.
                bool clWasLowest = fidelityManager != null
                    && fidelityManager.GetCurrentLevel() == FidelityManager.MaxLevel;
                bool firstAttemptCorrect = !normalHadWrongAttempt[currentIndex];
                triggerImmediateHard = clWasLowest && firstAttemptCorrect;
            }

            bool willFinish = false;
            if (currentDifficulty == "hard")
                willFinish = ComputeIsLastRemainingAfterThisHard();

            ShowFeedback("CORRECT", Color.green);
            if (SFXManager.Instance != null) SFXManager.Instance.PlayCorrect();

            if (willFinish)
                EnterFinishedState();
            else
                SetNextMode();

            if (triggerImmediateHard)
                immediateHardCoroutine = StartCoroutine(DelayedImmediateHardSwitch());
        }
        else
        {
            if (currentDifficulty == "normal")
                normalHadWrongAttempt[currentIndex] = true;

            ShowFeedback("WRONG", Color.red);
            if (SFXManager.Instance != null) SFXManager.Instance.PlayWrong();
            selectedAnswers.Clear();
            studentAnswers[currentIndex] = new List<int>();
            UpdateSelectionDisplay();
        }
    }

    private void SetConfirmMode()
    {
        awaitingConfirm = true;
        if (nextButtonText != null)
            nextButtonText.text = "Confirm";
    }

    private void SetNextMode()
    {
        awaitingConfirm = false;
        if (nextButtonText != null)
            nextButtonText.text = "Next >";
    }

    private void EnterFinishedState()
    {
        quizFinished = true;
        awaitingConfirm = false;
        if (nextButtonText != null)
            nextButtonText.text = "Finish";
    }

    private void OnFinish()
    {
        SaveResults();
        ShowThankYouScreen();
        Debug.Log("[QuizManager] Quiz finished - all questions completed.");
    }

    // -- Thank You Screen ----------------------------------------

    private void ShowThankYouScreen()
    {
        showingThankYou = true;

        questionText.text = "Thank you for completing the experiment!";
        questionImage.gameObject.SetActive(false);

        foreach (Button b in optionButtons)
            b.gameObject.SetActive(false);

        if (configButtonContainer != null)
            configButtonContainer.gameObject.SetActive(false);

        ClearFeedback();

        // Defer disabling the Next/Finish button by a frame so any coroutine
        // started by this same click (e.g. the button's own press animation)
        // has a chance to run before the GameObject goes inactive.
        StartCoroutine(DeactivateNextButtonNextFrame());

        prevButton.interactable = true;
    }

    private IEnumerator DeactivateNextButtonNextFrame()
    {
        yield return null;
        nextButton.gameObject.SetActive(false);
    }

    private void RestoreQuestionUI()
    {
        foreach (Button b in optionButtons)
            b.gameObject.SetActive(true);
        nextButton.gameObject.SetActive(true);
    }

    // -- Feedback Stamp ----------------------------------------

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.gameObject.SetActive(true);

        feedbackText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>" +
                            $"<size=120%><b>{message}</b></size></color>";

        feedbackText.outlineWidth = 0.3f;
        feedbackText.outlineColor = new Color32(
            (byte)(color.r * 0.5f * 255),
            (byte)(color.g * 0.5f * 255),
            (byte)(color.b * 0.5f * 255),
            255);
    }

    private void ClearFeedback()
    {
        if (feedbackText == null) return;
        feedbackText.gameObject.SetActive(false);
        feedbackText.text = "";
    }

    // -- Difficulty Flow ----------------------------------------

    private IEnumerator DelayedImmediateHardSwitch()
    {
        yield return new WaitForSeconds(upgradeDelaySeconds);
        immediateHardCoroutine = null;
        flowPhase = FlowPhase.ImmediateHardInterrupt;
        currentDifficulty = "hard";
        studentAnswers[currentIndex] = new List<int>();
        DisplayQuestion(currentIndex);
    }

    private bool AnyHardPendingRemaining()
    {
        for (int i = 0; i < hardPending.Length; i++)
            if (hardPending[i] && !hardShown[i]) return true;
        return false;
    }

    private bool ComputeIsLastRemainingAfterThisHard()
    {
        hardShown[currentIndex] = true;

        if (flowPhase == FlowPhase.ImmediateHardInterrupt)
        {
            if (currentIndex < quizData.questions.Length - 1)
                return false;

            return !AnyHardPendingRemaining();
        }

        if (flowPhase == FlowPhase.HardBackfill)
        {
            return !AnyHardPendingRemaining();
        }

        return false;
    }

    private void StartHardBackfillOrFinish()
    {
        backfillQueue = new List<int>();
        for (int i = 0; i < hardPending.Length; i++)
            if (hardPending[i] && !hardShown[i]) backfillQueue.Add(i);

        if (backfillQueue.Count == 0)
        {
            EnterFinishedState();
            return;
        }

        flowPhase = FlowPhase.HardBackfill;
        backfillPointer = 0;
        currentIndex = backfillQueue[0];
        currentDifficulty = "hard";
        studentAnswers[currentIndex] = new List<int>();
        DisplayQuestion(currentIndex);
    }

    private void AdvanceHardBackfill()
    {
        backfillPointer++;
        if (backfillPointer >= backfillQueue.Count)
        {
            EnterFinishedState();
            return;
        }

        currentIndex = backfillQueue[backfillPointer];
        currentDifficulty = "hard";
        studentAnswers[currentIndex] = new List<int>();
        DisplayQuestion(currentIndex);
    }

    private void RetreatHardBackfill()
    {
        if (backfillPointer <= 0)
        {
            flowPhase = FlowPhase.NormalRound;
            currentIndex = quizData.questions.Length - 1;
            currentDifficulty = "normal";
            DisplayQuestion(currentIndex);
            return;
        }

        backfillPointer--;
        currentIndex = backfillQueue[backfillPointer];
        currentDifficulty = "hard";
        DisplayQuestion(currentIndex);
    }

    // -- Config Buttons ----------------------------------------

    void SpawnConfigButtons(ExperimentConfig[] configs)
    {
        foreach (GameObject btn in spawnedConfigButtons)
            Destroy(btn);
        spawnedConfigButtons.Clear();

        if (configs == null || configs.Length <= 1)
        {
            if (configButtonContainer != null)
                configButtonContainer.gameObject.SetActive(false);
            return;
        }

        if (configButtonContainer != null)
            configButtonContainer.gameObject.SetActive(true);

        for (int i = 0; i < configs.Length; i++)
        {
            ExperimentConfig config = configs[i];
            GameObject btn = Instantiate(configButtonPrefab, configButtonContainer);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = config.label;

            int capturedIndex = i;
            TouchButton tb = btn.GetComponent<TouchButton>();
            if (tb != null)
                tb.onTouched.AddListener(() => OnConfigSelected(capturedIndex));

            spawnedConfigButtons.Add(btn);
        }
    }

    void OnConfigSelected(int configIndex)
    {
        if (currentExperimentConfigs == null ||
            configIndex < 0 || configIndex >= currentExperimentConfigs.Length) return;

        for (int i = 0; i < spawnedConfigButtons.Count; i++)
        {
            Image img = spawnedConfigButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == configIndex
                    ? new Color(0.6f, 1f, 0.6f)
                    : new Color(1f, 1f, 1f, 0f);
        }

        if (experimentConfigManager != null)
            experimentConfigManager.ApplyConfig(currentExperimentConfigs[configIndex]);

        Debug.Log($"[QuizManager] Config selected: {currentExperimentConfigs[configIndex].label}");
    }

    // -- Answer Selection ----------------------------------------

    public void SelectAnswer(int answerIndex)
    {
        if (!awaitingConfirm) return;

        if (selectedAnswers.Contains(answerIndex))
            selectedAnswers.Remove(answerIndex);
        else
            selectedAnswers.Add(answerIndex);

        studentAnswers[currentIndex] = new List<int>(selectedAnswers);
        UpdateSelectionDisplay();
    }

    void UpdateSelectionDisplay()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            Image btnImage = optionButtons[i].GetComponent<Image>();

            if (selectedAnswers.Contains(i))
            {
                btnText.fontStyle = FontStyles.Bold;
                btnImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
            else
            {
                btnText.fontStyle = FontStyles.Normal;
                btnImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    // -- Navigation ----------------------------------------

    public void PrevQuestion()
    {
        // Two input paths reach this button: QuizManager registers
        // Button.onClick, and QuizRaySelector calls TouchButton.TriggerButton
        // on the same object. TouchButton's own cooldown does not cover the
        // UI path, so one trigger press advanced two questions.
        if (Time.time - _lastNavInputTime < navInputCooldown) return;
        _lastNavInputTime = Time.time;
        if (showingThankYou)
        {
            showingThankYou = false;
            RestoreQuestionUI();
            quizFinished = false;
            DisplayQuestion(currentIndex);
            return;
        }

        SaveResults();
        FindFirstObjectByType<ResetManager>().ResetAll();

        if (flowPhase == FlowPhase.HardBackfill)
        {
            RetreatHardBackfill();
            return;
        }

        if (flowPhase == FlowPhase.ImmediateHardInterrupt)
        {
            flowPhase = FlowPhase.NormalRound;
            currentDifficulty = "normal";
            DisplayQuestion(currentIndex);
            return;
        }

        if (currentIndex > 0)
        {
            currentIndex--;
            currentDifficulty = "normal";
            DisplayQuestion(currentIndex);
        }
    }

    public void NextQuestion()
    {
        SaveResults();
        FindFirstObjectByType<ResetManager>().ResetAll();

        if (flowPhase == FlowPhase.ImmediateHardInterrupt)
        {
            flowPhase = FlowPhase.NormalRound;
            currentDifficulty = "normal";

            if (currentIndex < quizData.questions.Length - 1)
            {
                currentIndex++;
                DisplayQuestion(currentIndex);
            }
            else
            {
                StartHardBackfillOrFinish();
            }
            return;
        }

        if (flowPhase == FlowPhase.HardBackfill)
        {
            AdvanceHardBackfill();
            return;
        }

        if (currentIndex < quizData.questions.Length - 1)
        {
            currentIndex++;
            currentDifficulty = "normal";
            DisplayQuestion(currentIndex);
        }
        else
        {
            StartHardBackfillOrFinish();
        }
    }

    // -- Helpers ----------------------------------------

    IEnumerator LoadImage(string imagePath)
    {
        string fullPath = "file://" + Path.Combine(
            Application.streamingAssetsPath, "Images", imagePath + ".png");

        using (UnityEngine.Networking.UnityWebRequest request =
               UnityEngine.Networking.UnityWebRequestTexture.GetTexture(fullPath))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D tex = ((UnityEngine.Networking.DownloadHandlerTexture)
                    request.downloadHandler).texture;
                questionImage.sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
            }
            else
            {
                Debug.LogWarning("[QuizManager] Image not found: " + fullPath);
                questionImage.gameObject.SetActive(false);
            }
        }
    }

    bool IsAnswerCorrect(List<int> selected, int[] correct)
    {
        if (selected.Count != correct.Length) return false;
        List<int> sortedSelected = new List<int>(selected);
        sortedSelected.Sort();
        List<int> sortedCorrect = new List<int>(correct);
        sortedCorrect.Sort();
        return sortedSelected.SequenceEqual(sortedCorrect);
    }

    public void SaveResults()
    {
        string result = "{\n  \"answers\": [";
        for (int i = 0; i < studentAnswers.Length; i++)
        {
            Question q = quizData.questions[i];
            string diffLabel = hardShown[i] ? "hard" : "normal";
            DifficultySetting d = hardShown[i] ? q.difficulties.hard : q.difficulties.normal;

            bool isCorrect = IsAnswerCorrect(studentAnswers[i], d.correctAnswer);
            string selectedStr = string.Join(",", studentAnswers[i]);

            result += "\n    {\"questionId\": " + q.id +
                      ", \"difficulty\": \"" + diffLabel + "\"" +
                      ", \"selected\": [" + selectedStr + "]" +
                      ", \"correct\": " + (isCorrect ? "true" : "false") + "}";
            if (i < studentAnswers.Length - 1) result += ",";
        }
        result += "\n  ]\n}";

        string savePath = Path.Combine(Application.persistentDataPath, "quiz_results.json");
        File.WriteAllText(savePath, result);
        Debug.Log("[QuizManager] Results saved to: " + savePath);
    }
    public DifficultySetting GetCurrentDifficultySettingPublic()
    {
        if (quizData == null) return null;
        return GetCurrentDifficultySetting();
    }

    // -- Wizard-of-Oz Jump ----------------------------------------

    public bool IsLoaded() { return quizData != null && quizData.questions != null; }
    public int GetQuestionCount() { return IsLoaded() ? quizData.questions.Length : 0; }
    public int GetCurrentIndex() { return currentIndex; }
    public bool IsCurrentHard() { return currentDifficulty == "hard"; }
    public string GetFlowPhaseName() { return showingThankYou ? "ThankYou" : flowPhase.ToString(); }

    /// <summary>Short label for a question version, e.g. "2-1". Falls back to the id.</summary>
    public string GetQuestionLabel(int index, bool hard)
    {
        if (!IsLoaded() || index < 0 || index >= quizData.questions.Length) return "?";
        Question q = quizData.questions[index];
        DifficultySetting d = hard ? q.difficulties.hard : q.difficulties.normal;
        if (d != null && !string.IsNullOrEmpty(d.imagePath)) return d.imagePath;
        return q.id + (hard ? "H" : "N");
    }

    /// <summary>
    /// Jump straight to one question version, for the experimenter.
    ///
    /// Keeps the existing answer records and difficulty flow. The only state it
    /// touches is the one a result file can actually hold: each question stores
    /// ONE answer slot, recorded as either normal or hard. So:
    ///   - jumping to hard marks that question's hard version as shown, and it
    ///     will not be offered again by the immediate switch or the backfill;
    ///   - jumping to the OTHER difficulty than the slot currently records
    ///     clears that slot, because the old answer belongs to a different
    ///     question text.
    /// </summary>
    public void JumpTo(int index, bool hard)
    {
        if (!IsLoaded()) return;
        if (index < 0 || index >= quizData.questions.Length) return;
        if (hard && quizData.questions[index].difficulties.hard == null) return;

        if (immediateHardCoroutine != null)
        {
            StopCoroutine(immediateHardCoroutine);
            immediateHardCoroutine = null;
        }

        if (showingThankYou)
        {
            showingThankYou = false;
            RestoreQuestionUI();
        }
        quizFinished = false;

        SaveResults();
        ResetManager rm = FindFirstObjectByType<ResetManager>();
        if (rm != null) rm.ResetAll();

        bool slotWasHard = hardShown[index];
        if (slotWasHard != hard)
            studentAnswers[index] = new List<int>();

        if (hard)
        {
            hardPending[index] = true;
            hardShown[index] = true;
            // Same phase the automatic switch uses: Next goes on to the
            // following normal question, or to the backfill after the last one.
            flowPhase = FlowPhase.ImmediateHardInterrupt;
        }
        else
        {
            hardShown[index] = false;
            flowPhase = FlowPhase.NormalRound;
        }

        backfillQueue.Clear();
        backfillPointer = -1;

        currentIndex = index;
        currentDifficulty = hard ? "hard" : "normal";
        DisplayQuestion(currentIndex);

        Debug.Log("[QuizManager] Wizard jump to " + GetQuestionLabel(index, hard) +
                  " (" + currentDifficulty + ")");
    }
}