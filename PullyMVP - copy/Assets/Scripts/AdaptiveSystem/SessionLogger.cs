using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes two CSV files per session, in persistentDataPath/CognitiveLoadData/:
///
///   participant_{id}_group{X}_{date_time}_summary.csv
///     One row each time a question appears on screen, in the order it
///     appeared. Reopening a question (e.g. via Prev) starts a new row, so the
///     rows read top to bottom are the participant's full path. The practice
///     question is Order 0. A TOTAL row (practice excluded) is added at the end.
///
///   participant_{id}_group{X}_{date_time}_events.csv
///     One row only when something happens (question shown, answer, support
///     change, Play pressed, ...). Use it to find moments for cued recall.
///     The last column, UnixMs, matches the eye-tracking files' timestamps.
///
/// The date-time in the names means a repeated participant ID never
/// overwrites an earlier file. Both files are flushed after every row.
///
/// Other scripts call the static methods below. If no SessionLogger is in the
/// scene the calls do nothing.
/// </summary>
[DefaultExecutionOrder(-100)] // open the files before other scripts log
public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance { get; private set; }

    [Header("Participant")]
    [Tooltip("The participant ID is the one set on ExperimentLogger. It is not " +
             "typed here, so there is only one ID to change per participant.")]
    public ExperimentLogger experimentLogger;

    [Header("References")]
    public FidelityManager fidelityManager;
    public QuizManager quizManager;

    [Header("Read Only")]
    [SerializeField] private string summaryPath = "";
    [SerializeField] private string eventsPath = "";

    private StreamWriter summaryWriter;
    private StreamWriter eventsWriter;
    private float sessionStartTime = 0f;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // -- One question visit (one summary row) ---------------------------------

    private class Visit
    {
        public string order = "";
        public string question = "";
        public string difficulty = "";
        public bool practice;
        public float start;          // session seconds
        public float duration;
        public int attempts;
        public string firstTryCorrect = "";  // "", "1" or "0"
        public float clWeighted;     // sum of CL * dt
        public float clTime;         // sum of dt
        public float clMax;
        public float timeL2, timeL1, timeL0, timeAutoNoAid;
        public int supportChanges, requests, playPresses;
    }

    private Visit current;
    private Visit total = new Visit();
    private int firstTryCorrectCount = 0;
    private bool totalHasRows = false;
    private int orderCounter = 0;

    public int ParticipantId
    {
        get { return experimentLogger != null ? experimentLogger.participantId : 0; }
    }

    // -- Lifecycle ------------------------------------------------------------

    private void Reset()
    {
        if (experimentLogger == null) experimentLogger = FindFirstObjectByType<ExperimentLogger>();
        if (fidelityManager == null) fidelityManager = GetComponent<FidelityManager>();
        if (quizManager == null) quizManager = FindFirstObjectByType<QuizManager>();
    }

    private void Awake()
    {
        Instance = this;
        if (fidelityManager == null) fidelityManager = GetComponent<FidelityManager>();
        if (quizManager == null) quizManager = FindFirstObjectByType<QuizManager>();
        if (experimentLogger == null) experimentLogger = FindFirstObjectByType<ExperimentLogger>();
        if (experimentLogger == null)
            Debug.LogWarning("[SessionLogger] ExperimentLogger not found - participant ID will be 0.");
        Open();
    }

    private void Open()
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, $"Participant_{experimentLogger.participantId}");
            Directory.CreateDirectory(folder);

            string group = fidelityManager != null ? fidelityManager.GetGroupLabel() : "X";
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
            string baseName = "participant_" + ParticipantId + "_group" + group + "_" + stamp;

            summaryPath = Path.Combine(folder, baseName + "_summary.csv");
            eventsPath = Path.Combine(folder, baseName + "_events.csv");

            summaryWriter = new StreamWriter(summaryPath, false, new UTF8Encoding(false));
            summaryWriter.WriteLine(
                "Order,Question,Difficulty,Start,Duration,Attempts,FirstTryCorrect," +
                "CL_mean,CL_max,Time_L2,Time_L1,Time_L0,Time_AutoNoAid," +
                "SupportChanges,Requests,PlayPresses");
            summaryWriter.Flush();

            eventsWriter = new StreamWriter(eventsPath, false, new UTF8Encoding(false));
            eventsWriter.WriteLine("Time,Question,Event,Source,CL,Level,Detail,UnixMs");
            eventsWriter.Flush();

            sessionStartTime = Time.realtimeSinceStartup;
            Debug.Log("[SessionLogger] Logging to: " + summaryPath + " and " + eventsPath);
        }
        catch (Exception e)
        {
            summaryWriter = null;
            eventsWriter = null;
            Debug.LogError("[SessionLogger] Could not open log files: " + e.Message);
        }
    }

    private void Start()
    {
        WriteEvent("SessionStart", "Setup", "");
    }

    private void Update()
    {
        if (current == null) return;

        float dt = Time.unscaledDeltaTime;
        current.duration += dt;

        if (fidelityManager == null) return;

        float cl = fidelityManager.currentCLScore;
        current.clWeighted += cl * dt;
        current.clTime += dt;
        if (cl > current.clMax) current.clMax = cl;

        switch (fidelityManager.GetCurrentLevel())
        {
            case 2: current.timeL2 += dt; break;
            case 1: current.timeL1 += dt; break;
            case 0: current.timeL0 += dt; break;
            default: current.timeAutoNoAid += dt; break;
        }
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>A general event row. Also updates the summary counters.</summary>
    public static void Log(string evt, string source, string detail)
    {
        if (Instance == null) return;
        Instance.CountForSummary(evt, source);
        Instance.WriteEvent(evt, source, detail);
    }

    /// <summary>Called by QuizManager every time a question is displayed.</summary>
    public static void QuestionShown()
    {
        if (Instance == null) return;
        Instance.EndVisit();
        Instance.BeginVisit();
        Instance.WriteEvent("QuestionShown", "", "");
    }

    /// <summary>Called by QuizManager on every Confirm with an answer selected.</summary>
    public static void Answer(bool correct, string selected)
    {
        if (Instance == null) return;
        Visit v = Instance.current;
        if (v != null)
        {
            if (v.attempts == 0) v.firstTryCorrect = correct ? "1" : "0";
            v.attempts++;
        }
        Instance.WriteEvent("Answer", "Participant",
            (correct ? "correct" : "wrong") + ", selected " + selected);
    }

    /// <summary>Called by QuizManager when the participant presses Finish.</summary>
    public static void QuizFinished()
    {
        if (Instance == null) return;
        Instance.EndVisit();
        Instance.WriteEvent("QuizFinished", "Participant", "");
    }

    /// <summary>Readable name of a FidelityManager display code.</summary>
    public static string LevelName(int code)
    {
        switch (code)
        {
            case 2: return "L2";
            case 1: return "L1";
            case 0: return "L0";
            default: return "Auto";   // auto setup without aid (group B only)
        }
    }

    // -- Summary --------------------------------------------------------------

    private void CountForSummary(string evt, string source)
    {
        if (current == null) return;
        // A reset at a question boundary is logged as an event but is not a
        // support change made during the question.
        if (source == "QuestionReset") return;

        if (evt == "SupportChanged")
        {
            current.supportChanges++;
            if (source == "LearnerRequest") current.requests++;
        }
        else if (evt == "PlayPressed")
        {
            current.playPresses++;
        }
    }

    private void BeginVisit()
    {
        current = new Visit();
        current.start = Now();

        if (quizManager != null && quizManager.IsLoaded())
        {
            int i = quizManager.GetCurrentIndex();
            bool hard = quizManager.IsCurrentHard();
            current.practice = quizManager.IsPractice(i);
            current.question = quizManager.GetQuestionLabel(i, hard);
            current.difficulty = hard ? "hard" : "normal";
        }

        current.order = current.practice ? "0" : (++orderCounter).ToString(Inv);
    }

    private void EndVisit()
    {
        if (current == null) return;
        Visit v = current;
        current = null;

        WriteSummaryRow(v.order, v);

        if (v.practice) return;

        // Add to the TOTAL row (practice excluded).
        if (!totalHasRows) { total.start = v.start; totalHasRows = true; }
        total.duration += v.duration;
        total.attempts += v.attempts;
        if (v.firstTryCorrect == "1") firstTryCorrectCount++;
        total.clWeighted += v.clWeighted;
        total.clTime += v.clTime;
        if (v.clMax > total.clMax) total.clMax = v.clMax;
        total.timeL2 += v.timeL2;
        total.timeL1 += v.timeL1;
        total.timeL0 += v.timeL0;
        total.timeAutoNoAid += v.timeAutoNoAid;
        total.supportChanges += v.supportChanges;
        total.requests += v.requests;
        total.playPresses += v.playPresses;
    }

    private void WriteSummaryRow(string order, Visit v)
    {
        if (summaryWriter == null) return;

        float clMean = v.clTime > 0f ? v.clWeighted / v.clTime : 0f;

        StringBuilder sb = new StringBuilder(128);
        sb.Append(order).Append(',');
        sb.Append(Csv(v.question)).Append(',');
        sb.Append(v.difficulty).Append(',');
        sb.Append(Clock(v.start)).Append(',');
        sb.Append(v.duration.ToString("F1", Inv)).Append(',');
        sb.Append(v.attempts.ToString(Inv)).Append(',');
        sb.Append(v.firstTryCorrect).Append(',');
        sb.Append(clMean.ToString("F1", Inv)).Append(',');
        sb.Append(v.clMax.ToString("F1", Inv)).Append(',');
        sb.Append(v.timeL2.ToString("F1", Inv)).Append(',');
        sb.Append(v.timeL1.ToString("F1", Inv)).Append(',');
        sb.Append(v.timeL0.ToString("F1", Inv)).Append(',');
        sb.Append(v.timeAutoNoAid.ToString("F1", Inv)).Append(',');
        sb.Append(v.supportChanges.ToString(Inv)).Append(',');
        sb.Append(v.requests.ToString(Inv)).Append(',');
        sb.Append(v.playPresses.ToString(Inv));

        try
        {
            summaryWriter.WriteLine(sb.ToString());
            summaryWriter.Flush();
        }
        catch (Exception e) { Debug.LogError("[SessionLogger] Summary write failed: " + e.Message); }
    }

    private void WriteTotalRow()
    {
        if (!totalHasRows) return;
        // In the TOTAL row FirstTryCorrect is the number of question
        // appearances answered correctly on the first try.
        total.question = "";
        total.difficulty = "";
        total.firstTryCorrect = firstTryCorrectCount.ToString(Inv);
        WriteSummaryRow("TOTAL", total);
    }

    // -- Events ---------------------------------------------------------------

    private void WriteEvent(string evt, string source, string detail)
    {
        if (eventsWriter == null) return;

        string question = "";
        if (quizManager != null && quizManager.IsLoaded())
        {
            int i = quizManager.GetCurrentIndex();
            question = quizManager.GetQuestionLabel(i, quizManager.IsCurrentHard());
        }

        string cl = "", level = "";
        if (fidelityManager != null)
        {
            cl = fidelityManager.currentCLScore.ToString("F1", Inv);
            level = LevelName(fidelityManager.GetCurrentLevel());
        }

        StringBuilder sb = new StringBuilder(96);
        sb.Append(Clock(Now())).Append(',');
        sb.Append(Csv(question)).Append(',');
        sb.Append(Csv(evt)).Append(',');
        sb.Append(Csv(source)).Append(',');
        sb.Append(cl).Append(',');
        sb.Append(level).Append(',');
        sb.Append(Csv(detail)).Append(',');
        sb.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(Inv));

        try
        {
            eventsWriter.WriteLine(sb.ToString());
            eventsWriter.Flush();
        }
        catch (Exception e) { Debug.LogError("[SessionLogger] Event write failed: " + e.Message); }
    }

    // -- Helpers --------------------------------------------------------------

    private float Now() { return Time.realtimeSinceStartup - sessionStartTime; }

    /// <summary>Session time as mm:ss.</summary>
    private static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60).ToString("00", Inv) + ":" + (s % 60).ToString("00", Inv);
    }

    /// <summary>Quote a field only when it contains a comma, quote or line break.</summary>
    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private bool closed = false;

    private void Close()
    {
        if (closed) return;
        closed = true;

        EndVisit();
        WriteTotalRow();
        WriteEvent("SessionEnd", "Setup", "");

        try { summaryWriter?.Flush(); summaryWriter?.Close(); } catch (Exception) { }
        try { eventsWriter?.Flush(); eventsWriter?.Close(); } catch (Exception) { }
        summaryWriter = null;
        eventsWriter = null;
    }

    private void OnApplicationQuit() { Close(); }

    private void OnDestroy()
    {
        Close();
        if (Instance == this) Instance = null;
    }
}
