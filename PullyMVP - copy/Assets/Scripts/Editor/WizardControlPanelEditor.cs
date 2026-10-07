using UnityEngine;
using UnityEditor;

/// <summary>
/// Inspector buttons for WizardControlPanel.
/// MUST be inside a folder named "Editor" (e.g. Assets/Scripts/Editor/).
/// </summary>
[CustomEditor(typeof(WizardControlPanel))]
public class WizardControlPanelEditor : Editor
{
    private static readonly Color ActiveColor = new Color(0.45f, 0.9f, 0.45f);
    private static readonly Color PendingColor = new Color(1f, 0.85f, 0.4f);

    public override bool RequiresConstantRepaint()
    {
        // Keep the status lines live while the experiment runs.
        return Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WizardControlPanel panel = (WizardControlPanel)target;
        QuizManager qm = panel.quizManager;
        FidelityManager fm = panel.fidelityManager;

        // -- Experiment group (editable before Play only) --
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Experiment Group", EditorStyles.boldLabel);

        if (fm == null)
        {
            EditorGUILayout.HelpBox("FidelityManager not assigned.", MessageType.Warning);
        }
        else
        {
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            EditorGUI.BeginChangeCheck();
            ExperimentGroup g = (ExperimentGroup)EditorGUILayout.EnumPopup("Group", fm.group);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(fm, "Change experiment group");
                fm.group = g;
                EditorUtility.SetDirty(fm);
            }
            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox(GroupHint(fm.group) +
                    "\nSet the participant ID on ExperimentLogger before pressing Play.",
                    MessageType.Info);
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Buttons appear in Play mode.", MessageType.Info);
            return;
        }

        // -- Status --
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(panel.DescribeQuestion() + "\n" + panel.DescribeLevel(),
                                MessageType.None);

        // -- Baseline calibration music --
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Baseline Calibration", EditorStyles.boldLabel);

        if (panel.sfxManager == null)
        {
            EditorGUILayout.HelpBox("SFXManager not found on this GameObject.", MessageType.Warning);
        }
        else
        {
            bool playing = panel.IsBaselineMusicPlaying();
            EditorGUILayout.BeginHorizontal();
            Color old = GUI.backgroundColor;
            if (playing) GUI.backgroundColor = ActiveColor;
            if (GUILayout.Button(playing ? "Music playing" : "Start baseline music",
                                 GUILayout.Height(24)))
                panel.StartBaselineMusic();
            GUI.backgroundColor = old;
            if (GUILayout.Button("Stop (fade out)", GUILayout.Height(24)))
                panel.StopBaselineMusic();
            EditorGUILayout.EndHorizontal();
        }

        // -- Questions --
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Jump to Question", EditorStyles.boldLabel);

        if (qm == null || !qm.IsLoaded())
        {
            EditorGUILayout.HelpBox("Waiting for questions.json...", MessageType.Warning);
        }
        else
        {
            int count = qm.GetQuestionCount();
            int cur = qm.GetCurrentIndex();
            bool curHard = qm.IsCurrentHard();

            for (int i = 0; i < count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                DrawJumpButton(panel, qm, i, false, cur == i && !curHard);
                if (!qm.IsPractice(i))
                    DrawJumpButton(panel, qm, i, true, cur == i && curHard);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("< Prev")) panel.Prev();
            if (GUILayout.Button("Next >")) panel.Next();
            EditorGUILayout.EndHorizontal();
        }

        // -- Support controls, by group --
        if (fm == null) return;
        EditorGUILayout.Space(4);

        switch (fm.group)
        {
            case ExperimentGroup.A_NoSupport:
                EditorGUILayout.LabelField("Support (group A)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Fixed at Level 2 (manual setup, no aid). " +
                                        "No support controls.", MessageType.None);
                break;

            case ExperimentGroup.B_LearnerControl:
                DrawGroupB(panel, fm);
                break;

            case ExperimentGroup.C_Adaptive:
                DrawGroupC(panel, fm);
                break;
        }
    }

    // -- Group B: learner requests --

    private void DrawGroupB(WizardControlPanel panel, FidelityManager fm)
    {
        EditorGUILayout.LabelField("Learner Requests (group B)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Press only when the participant asks out loud. " +
                                "Every press is logged as LearnerRequest.",
                                MessageType.None);

        bool pending = fm.HasPendingLevelChange();
        bool aid = pending ? fm.GetPendingAid() : fm.IsAidOn();
        bool auto = pending ? fm.GetPendingAutoSetup() : fm.IsLowFidelity();

        EditorGUILayout.BeginHorizontal();
        DrawToggleButton("Aid ON", aid, pending && aid != fm.IsAidOn(),
                         () => panel.SetAid(true));
        DrawToggleButton("Aid OFF", !aid, pending && aid != fm.IsAidOn(),
                         () => panel.SetAid(false));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawToggleButton("Auto setup ON", auto, pending && auto != fm.IsLowFidelity(),
                         () => panel.SetAutoSetup(true));
        DrawToggleButton("Auto setup OFF", !auto, pending && auto != fm.IsLowFidelity(),
                         () => panel.SetAutoSetup(false));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Shortcuts", EditorStyles.miniBoldLabel);
        DrawLevelRow(panel, fm.GetCurrentLevel());
    }

    // -- Group C: override --

    private void DrawGroupC(WizardControlPanel panel, FidelityManager fm)
    {
        EditorGUILayout.LabelField("Override (group C, only if the classifier fails)",
                                   EditorStyles.boldLabel);
        DrawLevelRow(panel, fm.GetCurrentLevel());

        if (fm.manualMode)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = PendingColor;
            if (GUILayout.Button("Return to CL control", GUILayout.Height(24)))
                panel.ReturnToCLControl();
            GUI.backgroundColor = old;
        }
    }

    // -- Helpers --

    private static string GroupHint(ExperimentGroup g)
    {
        switch (g)
        {
            case ExperimentGroup.A_NoSupport:
                return "A: fixed Level 2. CL is logged and used only for the hard-question rule.";
            case ExperimentGroup.B_LearnerControl:
                return "B: participant asks out loud; you press Aid / Auto setup. " +
                       "CL is logged and used only for the hard-question rule.";
            default:
                return "C: CL classifier drives Level 2/1/0.";
        }
    }

    private void DrawLevelRow(WizardControlPanel panel, int current)
    {
        EditorGUILayout.BeginHorizontal();
        DrawLevelButton(panel, 2, "L2  no aid", current);
        DrawLevelButton(panel, 1, "L1  + aid", current);
        DrawLevelButton(panel, 0, "L0  auto + aid", current);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToggleButton(string label, bool active, bool pending, System.Action onClick)
    {
        Color old = GUI.backgroundColor;
        if (active) GUI.backgroundColor = pending ? PendingColor : ActiveColor;
        if (GUILayout.Button(label + (active && pending ? " (queued)" : ""), GUILayout.Height(24)))
            onClick();
        GUI.backgroundColor = old;
    }

    private void DrawJumpButton(WizardControlPanel panel, QuizManager qm,
                                int index, bool hard, bool isCurrent)
    {
        string label = qm.GetQuestionLabel(index, hard) +
            (qm.IsPractice(index) ? "  practice" : (hard ? "  hard" : "  normal"));
        Color old = GUI.backgroundColor;
        if (isCurrent) GUI.backgroundColor = ActiveColor;
        if (GUILayout.Button(label, GUILayout.Height(24)))
            panel.JumpTo(index, hard);
        GUI.backgroundColor = old;
    }

    private void DrawLevelButton(WizardControlPanel panel, int level, string label, int current)
    {
        Color old = GUI.backgroundColor;
        if (level == current) GUI.backgroundColor = ActiveColor;
        if (GUILayout.Button(label, GUILayout.Height(24)))
            panel.SetLevel(level);
        GUI.backgroundColor = old;
    }
}
