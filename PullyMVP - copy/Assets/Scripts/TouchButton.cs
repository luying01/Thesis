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

    public void TriggerButton()
    {
        if (isOnCooldown) return;
        Debug.Log("Button triggered: " + gameObject.name);
        onTouched.Invoke();
        StartCoroutine(StartCooldown());
    }

    System.Collections.IEnumerator StartCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldownTime);
        isOnCooldown = false;
    }
}