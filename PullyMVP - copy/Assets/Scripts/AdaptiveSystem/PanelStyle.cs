using UnityEngine;

/// <summary>
/// One shared description of how every panel in the scene looks - warnings and
/// the formula panel alike. Kept as an asset rather than as fields on each
/// component so that changing the look in one place changes it everywhere; two
/// panels that differ in styling read as two different kinds of information.
///
/// Create via: Assets > Create > PulleyMVP > Panel Style
/// </summary>
[CreateAssetMenu(fileName = "PanelStyle", menuName = "PulleyMVP/Panel Style")]
public class PanelStyle : ScriptableObject
{
    [Header("Colours")]
    public Color fillColor = new Color(0.941f, 0.890f, 0.776f, 1f);   // #F0E3C6
    public Color borderColor = new Color(0.863f, 0.780f, 0.604f, 1f); // #DCC79A
    public Color textColor = new Color(0.231f, 0.196f, 0.153f, 1f);   // #3B3227

    [Header("Shape")]
    [Tooltip("Corner radius, in units of half the panel height. 1 = fully rounded ends.")]
    [Range(0f, 1f)] public float cornerRadius = 0.35f;
    [Tooltip("Border thickness, in the same units as the radius.")]
    [Range(0f, 0.5f)] public float borderWidth = 0.05f;
    [Range(0f, 1f)] public float opacity = 1f;

    [Header("Text")]
    public float fontSize = 1.5f;
    [Tooltip("Space between the text and the panel edge, in world metres.")]
    public float paddingX = 0.03f;
    public float paddingY = 0.02f;

    [Header("Text Outline")]
    [Tooltip("VR optics wash out contrast. A little outline usually helps, but " +
             "too much swallows the glyph - start near zero and add only if needed.")]
    [Range(0f, 0.5f)] public float textOutlineWidth = 0f;
    public Color textOutlineColor = new Color(1f, 1f, 1f, 1f);

    /// <summary>Push these values onto a material instance of UI/RoundedPanel.</summary>
    public void ApplyTo(Material material, float aspect)
    {
        if (material == null) return;
        material.SetColor("_FillColor", fillColor);
        material.SetColor("_BorderColor", borderColor);
        material.SetFloat("_Radius", cornerRadius);
        material.SetFloat("_BorderWidth", borderWidth);
        material.SetFloat("_Aspect", Mathf.Max(0.01f, aspect));
        material.SetFloat("_Alpha", opacity);
    }
}
