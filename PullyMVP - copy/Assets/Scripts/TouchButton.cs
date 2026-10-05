using UnityEngine;
using UnityEngine.Events;

public class TouchButton : MonoBehaviour
{
    [Header("Settings")]
    public float cooldownTime = 1.0f;
    public UnityEvent onTouched;
    private bool isOnCooldown = false;

    void Start()
    {
        // Add Box Collider for ray detection
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        // Match collider to button size
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            col.size = new Vector3(rect.rect.width, rect.rect.height, 0.01f);
            col.isTrigger = false; // Must be false for Physics.Raycast
        }
    }

    // Whenever this button becomes active again (e.g. the panel is re-shown
    // after being hidden while playing), clear any leftover cooldown lock -
    // a cooldown coroutine gets silently killed if the GameObject was
    // deactivated mid-wait, which would otherwise leave isOnCooldown stuck true.
    void OnEnable()
    {
        isOnCooldown = false;
    }

    public void TriggerButton()
    {
        Debug.Log($"[TouchButton] TriggerButton called on {gameObject.name}, isOnCooldown={isOnCooldown}");
        if (isOnCooldown) return;
        Debug.Log("Button triggered: " + gameObject.name);
        StartCoroutine(StartCooldown());

        // Click sound. SFXManager skips it if this same click also produced
        // a correct/wrong sound (e.g. the Confirm button), so they never overlap.
        UnityEngine.UI.Button uiButton = GetComponent<UnityEngine.UI.Button>();
        bool interactable = uiButton == null || uiButton.IsInteractable();
        if (interactable && SFXManager.Instance != null) SFXManager.Instance.RequestConfirm();

        onTouched.Invoke();
    }

    System.Collections.IEnumerator StartCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldownTime);
        isOnCooldown = false;
    }
}