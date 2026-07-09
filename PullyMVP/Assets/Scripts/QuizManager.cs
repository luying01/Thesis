using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;

// ©¤©¤ Data Classes ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
// Note: WeightAssignment, ExperimentConfig, Vector3Data defined in ExperimentConfigManager.cs

[System.Serializable]
public class DifficultySetting
{
    public string questionText;
    public string imagePath;
    public string[] options;
    public int[] correctAnswer;
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
    public ExperimentConfig[] experimentConfigs;
}

[System.Serializable]
public class QuestionList
{
    public Question[] questions;
}

// ©¤©¤ QuizManager ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

public class QuizManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI questionText;
    public Button[] optionButtons;
    public Image questionImage;
    public Button prevButton;
    public Button nextButton;

    [Header("Config Buttons")]
    public GameObject configButtonPrefab;
    public Transform configButtonContainer;

    [Header("References")]
    public ExperimentConfigManager experimentConfigManager;

    [Header("Selection Indicator")]
    public GameObject selectionRing;

    // ©¤©¤ Internal State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private QuestionList quizData;
    private int currentIndex = 0;
    private string currentDifficulty = "normal";
    private List<int> selectedAnswers = new List<int>();
    private List<int>[] studentAnswers;
    private List<GameObject> spawnedConfigButtons = new List<GameObject>();

    // ©¤©¤ Unity Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    void Start()
    {
        LoadQuestions();

        prevButton.onClick.AddListener(PrevQuestion);
        nextButton.onClick.AddListener(NextQuestion);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => SelectAnswer(index));
        }

        DisplayQuestion(currentIndex);
    }

    // ©¤©¤ Load ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    void LoadQuestions()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "questions.json");
        if (!File.Exists(path))
        {
            Debug.LogError("[QuizManager] questions.json not found at: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        quizData = JsonUtility.FromJson<QuestionList>(json);

        studentAnswers = new List<int>[quizData.questions.Length];
        for (int i = 0; i < studentAnswers.Length; i++)
            studentAnswers[i] = new List<int>();
    }

    // ©¤©¤ Display ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    void DisplayQuestion(int index)
    {
        Question q = quizData.questions[index];
        DifficultySetting d = currentDifficulty == "hard"
            ? q.difficulties.hard
            : q.difficulties.normal;

        // Question text
        questionText.text = q.id + ". " + d.questionText;

        // Answer buttons
        string[] labels = { "A", "B", "C", "D" };
        for (int i = 0; i < optionButtons.Length; i++)
        {
            TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            btnText.text = labels[i] + ". " + d.options[i];
        }

        // Image
        if (!string.IsNullOrEmpty(d.imagePath))
        {
            questionImage.gameObject.SetActive(true);
            StartCoroutine(LoadImage(d.imagePath));
        }
        else
        {
            questionImage.gameObject.SetActive(false);
        }

        // Restore previous selections
        selectedAnswers = new List<int>(studentAnswers[index]);
        UpdateSelectionDisplay();

        // Config buttons
        SpawnConfigButtons(q.experimentConfigs);

        // Nav buttons
        prevButton.interactable = (index > 0);
        nextButton.interactable = (index < quizData.questions.Length - 1);

        // Load default config (first config) for this question
        if (q.experimentConfigs != null && q.experimentConfigs.Length > 0)
            if (experimentConfigManager != null)
                experimentConfigManager.ApplyConfig(q.experimentConfigs[0]);
    }

    // ©¤©¤ Config Buttons ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    void SpawnConfigButtons(ExperimentConfig[] configs)
    {
        foreach (GameObject btn in spawnedConfigButtons)
            Destroy(btn);
        spawnedConfigButtons.Clear();

        if (configs == null || configs.Length == 0)
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
            btn.GetComponent<Button>().onClick.AddListener(() => OnConfigSelected(capturedIndex));

            spawnedConfigButtons.Add(btn);
        }
    }

    void OnConfigSelected(int configIndex)
    {
        Question q = quizData.questions[currentIndex];
        if (configIndex < 0 || configIndex >= q.experimentConfigs.Length) return;

        // Highlight selected button
        for (int i = 0; i < spawnedConfigButtons.Count; i++)
        {
            Image img = spawnedConfigButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == configIndex
                    ? new Color(0.6f, 1f, 0.6f)
                    : new Color(1f, 1f, 1f, 0f);
        }

        if (experimentConfigManager != null)
            experimentConfigManager.ApplyConfig(q.experimentConfigs[configIndex]);

        Debug.Log($"[QuizManager] Config selected: {q.experimentConfigs[configIndex].label}");
    }

    // ©¤©¤ Difficulty Switching ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void SetDifficulty(string difficulty)
    {
        if (currentDifficulty == difficulty) return;
        currentDifficulty = difficulty;
        DisplayQuestion(currentIndex);
        Debug.Log($"[QuizManager] Difficulty changed to: {difficulty}");
    }

    // ©¤©¤ Answer Selection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void SelectAnswer(int answerIndex)
    {
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

    // ©¤©¤ Navigation ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void PrevQuestion()
    {
        SaveResults();
        FindObjectOfType<ResetManager>().ResetAll();
        if (currentIndex > 0)
        {
            currentIndex--;
            DisplayQuestion(currentIndex);
        }
    }

    public void NextQuestion()
    {
        SaveResults();
        FindObjectOfType<ResetManager>().ResetAll();
        if (currentIndex < quizData.questions.Length - 1)
        {
            currentIndex++;
            DisplayQuestion(currentIndex);
        }
    }

    // ©¤©¤ Helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

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
            DifficultySetting d = currentDifficulty == "hard"
                ? q.difficulties.hard
                : q.difficulties.normal;

            bool isCorrect = IsAnswerCorrect(studentAnswers[i], d.correctAnswer);
            string selectedStr = string.Join(",", studentAnswers[i]);

            result += "\n    {\"questionId\": " + q.id +
                      ", \"difficulty\": \"" + currentDifficulty + "\"" +
                      ", \"selected\": [" + selectedStr + "]" +
                      ", \"correct\": " + (isCorrect ? "true" : "false") + "}";
            if (i < studentAnswers.Length - 1) result += ",";
        }
        result += "\n  ]\n}";

        string savePath = Path.Combine(Application.persistentDataPath, "quiz_results.json");
        File.WriteAllText(savePath, result);
        Debug.Log("[QuizManager] Results saved to: " + savePath);
    }
}