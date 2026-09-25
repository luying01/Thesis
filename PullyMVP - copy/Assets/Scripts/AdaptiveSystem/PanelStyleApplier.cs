using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a UGUI Image with the UI/RoundedPanel material from a shared
/// PanelStyle asset.
///
/// The aspect ratio has to be pushed to the material every time the rect
/// changes, otherwise the corner radius stretches with the panel and circular
/// corners turn into ellipses - which is exactly what happens on a panel that
/// resizes itself to fit its text.
///
/// Runs in edit mode too, so the look can be tuned without entering Play.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class PanelStyleApplier : MonoBehaviour
{
    [Tooltip("Shared look for every panel in the scene.")]
    public PanelStyle style;

    [Tooltip("Text elements tinted with the style's text colour. Leave empty " +
             "to manage their colours yourself.")]
    public TMPro.TMP_Text[] texts;

    [Tooltip("On: objects in front of the panel hide it, the same as its text. " +
             "Off: the backdrop is drawn through everything. Keep this matching " +
             "the text, or the backdrop and its words disappear separately.")]
    public bool occludedByScene = true;

    private Image image;
    private Material materialInstance;
    private RectTransform rect;
    private Vector2 lastSize = Vector2.zero;

    void OnEnable()
    {
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        EnsureMaterial();
        Apply();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        EnsureMaterial();
        Apply();
    }

    void Update()
    {
        if (rect == null) return;

        Vector2 size = rect.rect.size;
        if (size != lastSize)
        {
            lastSize = size;
            Apply();
        }
    }

    private void EnsureMaterial()
    {
        if (image == null) return;

        Shader shader = Shader.Find("UI/RoundedPanel");
        if (shader == null)
        {
            Debug.LogWarning("[PanelStyleApplier] Shader 'UI/RoundedPanel' not found on " +
                             gameObject.name + ". Leaving the Image's material alone.");
            return;
        }

        // One instance per applier, so two panels can differ if they are ever
        // given different styles.
        if (materialInstance == null || materialInstance.shader != shader)
        {
            materialInstance = new Material(shader);
            materialInstance.name = "RoundedPanel (instance)";
        }

        image.material = materialInstance;
    }

    /// <summary>Push the current style and rect onto the material. Cheap; safe to call often.</summary>
    public void Apply()
    {
        if (style == null || image == null || rect == null) return;
        if (materialInstance == null) EnsureMaterial();
        if (materialInstance == null) return;

        Vector2 size = rect.rect.size;
        float aspect = size.y > 0.0001f ? size.x / size.y : 1f;
        style.ApplyTo(materialInstance, aspect);
        materialInstance.SetFloat("_ZTest", occludedByScene
            ? (float)UnityEngine.Rendering.CompareFunction.LessEqual
            : (float)UnityEngine.Rendering.CompareFunction.Always);

        // The Image's own colour multiplies the shader output, so keep it white
        // or the fill colour from the style asset would be tinted twice.
        image.color = Color.white;

        if (texts == null) return;
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            texts[i].color = style.textColor;
            texts[i].outlineColor = style.textOutlineColor;
            texts[i].outlineWidth = style.textOutlineWidth;
        }
    }
}