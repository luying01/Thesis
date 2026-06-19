using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;

[System.Serializable]
public class Question
{
    public int id;
    public string questionText;
    public string imagePath;
    public string[] options;
    public int[] correctAnswer;   // Changed from int to int[] to support multiple correct answers
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
    public Button[] optionButtons;        // A, B, C, D
    public Image questionImage;
    public Button prevButton;
    public Button nextButton;

    [Header("Selection Indicator")]
    public GameObject selectionRing;      // Selection ring prefab

    private QuestionList quizData;
    private int currentIndex = 0;
    private List<int> selectedAnswers = new List<int>();  // Now stores multiple selections
    private List<int>[] studentAnswers;   // Record each question's answer(s)

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

    void LoadQuestions()
    {
        // Load JSON from StreamingAssets
        string path = Path.Combine(Application.streamingAssetsPath, "questions.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            quizData = JsonUtility.FromJson<QuestionList>(json);
            studentAnswers = new List<int>[quizData.questions.Length];
            // Initialize all answers to empty lists (unanswered)
            for (int i = 0; i < studentAnswers.Length; i++)
                studentAnswers[i] = new List<int>();
        }
        else
        {
            Debug.LogError("questions.json not found at: " + path);
        }
    }

    void DisplayQuestion(int index)
    {
        Question q = quizData.questions[index];

        // Display question text
        questionText.text = q.id + ". " + q.questionText;

        // Display options
        string[] labels = { "A", "B", "C", "D" };
        for (int i = 0; i < optionButtons.Length; i++)
        {
            TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            btnText.text = labels[i] + ". " + q.options[i];
        }

        // Display image if available
        if (!string.IsNullOrEmpty(q.imagePath))
        {
            questionImage.gameObject.SetActive(true);
            StartCoroutine(LoadImage(q.imagePath));
        }
        else
        {
            questionImage.gameObject.SetActive(false);
        }

        // Restore previous answer(s) if exists
        selectedAnswers = new List<int>(studentAnswers[index]);
        UpdateSelectionDisplay();

        // Update prev/next button state
        prevButton.interactable = (index > 0);
        nextButton.interactable = (index < quizData.questions.Length - 1);
    }

    IEnumerator LoadImage(string imagePath)
    {
        // Load image from StreamingAssets/Images/
        string fullPath = "file://" + Path.Combine(Application.streamingAssetsPath, "Images", imagePath + ".png");
        using (UnityEngine.Networking.UnityWebRequest request =
               UnityEngine.Networking.UnityWebRequestTexture.GetTexture(fullPath))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D tex = ((UnityEngine.Networking.DownloadHandlerTexture)request.downloadHandler).texture;
                questionImage.sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
            }
            else
            {
                Debug.LogWarning("Image not found: " + fullPath);
                questionImage.gameObject.SetActive(false);
            }
        }
    }

    public void SelectAnswer(int answerIndex)
    {
        // Toggle selection: if already selected, remove it; otherwise add it
        if (selectedAnswers.Contains(answerIndex))
        {
            selectedAnswers.Remove(answerIndex);
        }
        else
        {
            selectedAnswers.Add(answerIndex);
        }

        studentAnswers[currentIndex] = new List<int>(selectedAnswers);
        UpdateSelectionDisplay();
    }

    void UpdateSelectionDisplay()
    {
        // Highlight all selected buttons
        for (int i = 0; i < optionButtons.Length; i++)
        {
            TextMeshProUGUI btnText = optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            Image btnImage = optionButtons[i].GetComponent<Image>();

            if (selectedAnswers.Contains(i))
            {
                // Selected: bold text, light gray background
                btnText.fontStyle = FontStyles.Bold;
                btnImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
            else
            {
                // Not selected: normal
                btnText.fontStyle = FontStyles.Normal;
                btnImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    void PrevQuestion()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            DisplayQuestion(currentIndex);
        }
    }

    void NextQuestion()
    {
        if (currentIndex < quizData.questions.Length - 1)
        {
            currentIndex++;
            DisplayQuestion(currentIndex);
        }
    }

    // Check if two answer sets are exactly the same (order doesn't matter)
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
        // Save student answers to a JSON file
        string result = "{\n  \"answers\": [";
        for (int i = 0; i < studentAnswers.Length; i++)
        {
            Question q = quizData.questions[i];
            bool isCorrect = IsAnswerCorrect(studentAnswers[i], q.correctAnswer);

            string selectedStr = string.Join(",", studentAnswers[i]);

            result += "\n    {\"questionId\": " + q.id +
                      ", \"selected\": [" + selectedStr + "]" +
                      ", \"correct\": " + (isCorrect ? "true" : "false") + "}";
            if (i < studentAnswers.Length - 1) result += ",";
        }
        result += "\n  ]\n}";

        string savePath = Path.Combine(Application.persistentDataPath, "quiz_results.json");
        File.WriteAllText(savePath, result);
        Debug.Log("Results saved to: " + savePath);
    }
}