using UnityEngine;

/// <summary>
/// Generic hover feedback for any TouchButton hit by the quiz ray:
/// the button smoothly scales up while the ray points at it.
///
/// Added automatically by QuizRaySelector the first time a button is hovered,
/// so you do not need to add it by hand. Buttons that already have
/// PlayButtonRayTarget keep using that script instead.
///
/// Scale only, no colour tint: QuizManager already uses the Image colour of
/// answer and config buttons to show "selected", so tinting would clash.
/// </summary>
public class RayHoverEffect : MonoBehaviour
{
    [Tooltip("Scale while hovered, relative to the button's normal scale.")]
    public float hoverScale = 1.08f;
    [Tooltip("Seconds to blend in and out of the hover look.")]
    public float fadeTime = 0.08f;

    private Vector3 normalScale = Vector3.one;
    private bool captured;
    private bool hovered;
    private float blend;   // 0 = normal, 1 = fully hovered

    void OnEnable()
    {
        // Capture the resting scale once, then always restore to it.
        if (!captured)
        {
            normalScale = transform.localScale;
            captured = true;
        }
        hovered = false;
        blend = 0f;
        ApplyLook();
    }

    void OnDisable()
    {
        hovered = false;
        blend = 0f;
        ApplyLook();
    }

    public void SetHovered(bool value)
    {
        hovered = value;
    }

    void LateUpdate()
    {
        float target = hovered ? 1f : 0f;
        float step = fadeTime > 0.0001f ? Time.deltaTime / fadeTime : 1f;
        float next = Mathf.MoveTowards(blend, target, step);
        if (!Mathf.Approximately(next, blend))
        {
            blend = next;
            ApplyLook();
        }
    }

    private void ApplyLook()
    {
        transform.localScale = Vector3.Lerp(normalScale, normalScale * hoverScale, blend);
    }
}
