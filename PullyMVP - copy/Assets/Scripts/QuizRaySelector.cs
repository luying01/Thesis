using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class QuizRaySelector : MonoBehaviour
{
    [Header("References")]
    public Transform rayOrigin;              // Controller transform
    public LayerMask quizUILayer;            // Set to QuizUI layer
    public float rayDistance = 2.0f;         // Max ray distance

    [Header("Input")]
    public ActionBasedController controller; // The XR controller

    private LineRenderer lineRenderer;
    private TouchButton currentHovered;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    void Start()
    {
        // Setup line renderer for ray visualization
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.002f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = new Color(0, 0, 1, 0);
        lineRenderer.enabled = false;
    }

    void Update()
    {
        if (rayOrigin == null || controller == null) return;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        int count = Physics.RaycastNonAlloc(ray, hits, rayDistance, quizUILayer,
            QueryTriggerInteraction.UseGlobal);

        if (count == 0)
        {
            // Hide ray when not pointing at any quiz-layer surface
            lineRenderer.enabled = false;
            SetHovered(null);
            return;
        }

        // Look at everything the ray passes through, not just the first hit.
        // Read-only surfaces (e.g. the formula panel body) have a collider so
        // the ray shows while reading, but they must never block a real
        // button lying behind them.
        int nearestAny = -1;
        int nearestButton = -1;
        TouchButton button = null;

        for (int i = 0; i < count; i++)
        {
            if (nearestAny < 0 || hits[i].distance < hits[nearestAny].distance)
                nearestAny = i;

            TouchButton b = hits[i].collider.GetComponentInParent<TouchButton>();
            if (b != null && (nearestButton < 0 || hits[i].distance < hits[nearestButton].distance))
            {
                nearestButton = i;
                button = b;
            }
        }

        // The ray ends on the button it would press; otherwise on the nearest surface.
        int shown = nearestButton >= 0 ? nearestButton : nearestAny;
        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, rayOrigin.position);
        lineRenderer.SetPosition(1, hits[shown].point);

        SetHovered(button);
        if (button != null && controller.activateAction.action.WasPressedThisFrame())
        {
            Debug.Log("Ray selected: " + hits[nearestButton].collider.gameObject.name);
            button.TriggerButton();
        }
    }

    /// <summary>
    /// Track which button the ray is on and tell it, if it wants to know
    /// (only buttons with PlayButtonRayTarget react; answer buttons are unchanged).
    /// </summary>
    private void SetHovered(TouchButton button)
    {
        if (button == currentHovered) return;

        if (currentHovered != null)
        {
            PlayButtonRayTarget oldTarget = currentHovered.GetComponent<PlayButtonRayTarget>();
            if (oldTarget != null) oldTarget.SetHovered(false);
        }

        currentHovered = button;

        if (currentHovered != null)
        {
            PlayButtonRayTarget newTarget = currentHovered.GetComponent<PlayButtonRayTarget>();
            if (newTarget != null) newTarget.SetHovered(true);
        }
    }

    void OnDisable()
    {
        SetHovered(null);
        if (lineRenderer != null) lineRenderer.enabled = false;
    }
}
