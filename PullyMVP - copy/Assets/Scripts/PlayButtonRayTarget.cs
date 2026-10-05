using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the Play / "Show Forces Again" button easy to hit with the quiz ray.
/// Put it on the same GameObject as the button's TouchButton (PlayPanel/Button).
///
/// Why only this button: it starts hidden, so TouchButton.Start() sizes the
/// collider once, at the moment the button first appears - often before the
/// layout has computed the button's size, and never again when the label
/// changes length. Quiz answer buttons are visible from the start and sit
/// close together, so they are deliberately left untouched.
///
/// What it does:
///   1. Every frame, keeps the BoxCollider matched to the button's real rect,
///      plus a little padding, so the hit area always covers the button.
///   2. Shortens the TouchButton cooldown so a second press is not swallowed.
///   3. Hover feedback: while the quiz ray points at the button it is tinted
///      and slightly enlarged, so the student knows a press will register.
///      QuizRaySelector calls SetHovered().
/// </summary>
[RequireComponent(typeof(TouchButton))]
public class PlayButtonRayTarget : MonoBehaviour
{
    [Header("Hit area")]
    [Tooltip("Extra hit area around the button, as a fraction of its size on " +
             "each axis. 0.2 = 20% wider and 20% taller than the visible button.")]
    [Range(0f, 1f)] public float hitPadding = 0.2f;
    [Tooltip("Cooldown applied to this button's TouchButton, in seconds.")]
    public float cooldownTime = 0.3f;

    [Header("Hover feedback")]
    [Tooltip("Image tinted while the ray points at the button. Defaults to the " +
             "Image on this GameObject.")]
    public Image targetImage;
    public Color hoverColor = new Color(1f, 0.92f, 0.62f, 1f);   // warm highlight
    [Tooltip("Scale while hovered, relative to the button's normal scale.")]
    public float hoverScale = 1.08f;
    [Tooltip("Seconds to blend in and out of the hover look.")]
    public float fadeTime = 0.08f;

    private RectTransform rect;
    private BoxCollider box;
    private TouchButton touchButton;
    private Color normalColor = Color.white;
    private Vector3 normalScale = Vector3.one;
    private bool hovered;
    private float blend;           // 0 = normal, 1 = fully hovered
    private bool captured;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        touchButton = GetComponent<TouchButton>();
        box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = false;

        if (targetImage == null) targetImage = GetComponent<Image>();
        if (touchButton != null) touchButton.cooldownTime = cooldownTime;
    }

    void OnEnable()
    {
        // Capture the resting look once, then always restore to it.
        if (!captured)
        {
            if (targetImage != null) normalColor = targetImage.color;
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
        // 1. Hit area follows the real rect (after layout has run this frame).
        //    The collider lives in local space, so the hover scale-up is
        //    applied to it automatically.
        if (rect != null && box != null)
        {
            Vector2 size = rect.rect.size;
            if (size.x > 0.0001f && size.y > 0.0001f)
            {
                box.center = (Vector3)rect.rect.center;
                box.size = new Vector3(size.x * (1f + hitPadding),
                                       size.y * (1f + hitPadding),
                                       Mathf.Max(box.size.z, 0.01f));
            }
        }

        // 2. Keep the cooldown short even if something resets it.
        if (touchButton != null && !Mathf.Approximately(touchButton.cooldownTime, cooldownTime))
            touchButton.cooldownTime = cooldownTime;

        // 3. Hover feedback.
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
        if (targetImage != null)
            targetImage.color = Color.Lerp(normalColor, hoverColor, blend);
        transform.localScale = Vector3.Lerp(normalScale, normalScale * hoverScale, blend);
    }
}
