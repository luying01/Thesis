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

        // Cast ray from controller forward direction
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, quizUILayer))
        {
            // Show ray
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, rayOrigin.position);
            lineRenderer.SetPosition(1, hit.point);

            // Check if hitting a TouchButton
            TouchButton button = hit.collider.GetComponentInParent<TouchButton>();
            if (button != null)
            {
                currentHovered = button;

                // Check trigger press
                if (controller.activateAction.action.WasPressedThisFrame())
                {
                    Debug.Log("Ray selected: " + hit.collider.gameObject.name);
                    button.TriggerButton();
                }
            }
            else
            {
                currentHovered = null;
            }
        }
        else
        {
            // Hide ray when not pointing at quiz
            lineRenderer.enabled = false;
            currentHovered = null;
        }
    }
}
