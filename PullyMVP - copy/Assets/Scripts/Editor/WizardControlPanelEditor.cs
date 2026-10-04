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

    public override bool RequiresConstantRepaint()
    {
        // Keep the status lines live while the experiment runs.
        return Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WizardControlPanel panel = (WizardControlPanel)target;
        EditorGUILayout.Space(8);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Buttons appear in Play mode.", MessageType.Info);
            return;
        }

        QuizManager qm = panel.quizManager;
        FidelityManager fm = panel.fidelityManager;

        // ©¤©¤ Status ©¤©¤
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(panel.DescribeQuestion() + "\n" + panel.DescribeLevel(),
                                MessageType.None);

        // ©¤©¤ Questions ©¤©¤
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
                DrawJumpButton(panel, qm, i, true, cur == i && curHard);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("< Prev")) panel.Prev();
            if (GUILayout.Button("Next >")) panel.Next();
            EditorGUILayout.EndHorizontal();
        }

        // ©¤©¤ Fidelity level ©¤©¤
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Set Fidelity Level", EditorStyles.boldLabel);

        int level = fm != null ? fm.GetCurrentLevel() : -1;
        EditorGUILayout.BeginHorizontal();
        DrawLevelButton(panel, 2, "L2  no aid", level);
        DrawLevelButton(panel, 1, "L1  + aid", level);
        DrawLevelButton(panel, 0, "L0  low + aid", level);
        EditorGUILayout.EndHorizontal();

        if (fm != null && fm.manualMode)
        {
            if (GUILayout.Button("Return to CL control"))
                panel.ReturnToCLControl();
        }
    }

    private void DrawJumpButton(WizardControlPanel panel, QuizManager qm,
                                int index, bool hard, bool isCurrent)
    {
        string label = qm.GetQuestionLabel(index, hard) + (hard ? "  hard" : "  normal");
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