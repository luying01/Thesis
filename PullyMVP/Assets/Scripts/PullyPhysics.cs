using UnityEngine;

public class PulleyPhysics : MonoBehaviour
{
    [Header("References")]
    public PulleySystem pulleySystem;
    public Transform slotLeft;
    public Transform slotRight;
    public Transform hookLeft;
    public Transform hookRight;
    public Transform movablePulleyBottom;
    public Transform freeEndHook;

    [Header("Weight Chains")]
    public GameObject weightChainLeft;
    public GameObject weightChainRight;
    public GameObject weightChainLoad;
    public GameObject weightChainForce;

    [Header("Rope Settings")]
    public float totalRopeLength = 2f;
    public float totalRopeLengthMA2 = 1.0f;

    [Header("Read Only")]
    public float leftLength;
    public float rightLength;
    public float velocity = 0f;
    public float massLeft = 0f;
    public float massRight = 0f;

    private float g = 9.81f;
    private float loadY;
    private float forceY;
    private Transform fixedPulleySlotRef;

    void Start()
    {
        leftLength = totalRopeLength * 0.5f;
        rightLength = totalRopeLength * 0.5f;
        velocity = 0f;

        if (movablePulleyBottom != null)
            loadY = movablePulleyBottom.parent != null ?
                movablePulleyBottom.parent.position.y :
                movablePulleyBottom.position.y;
        if (freeEndHook != null)
            forceY = freeEndHook.position.y;
    }

    void FixedUpdate()
    {
        float MA = pulleySystem != null ? pulleySystem.GetMechanicalAdvantage() : 1f;

        RopeGrab ropeGrabLeft = hookLeft != null ? hookLeft.GetComponent<RopeGrab>() : null;
        RopeGrab ropeGrabRight = hookRight != null ? hookRight.GetComponent<RopeGrab>() : null;
        bool leftGrabbed = ropeGrabLeft != null && ropeGrabLeft.isGrabbed;
        bool rightGrabbed = ropeGrabRight != null && ropeGrabRight.isGrabbed;

        massLeft = weightChainLeft != null ? GetChainMass(weightChainLeft) : 0f;
        massRight = weightChainRight != null ? GetChainMass(weightChainRight) : 0f;

        if (MA <= 1f)
            UpdateAtwood(leftGrabbed, rightGrabbed);
        else
            UpdateMovablePulley(MA, leftGrabbed, rightGrabbed);
    }

    private void UpdateAtwood(bool leftGrabbed, bool rightGrabbed)
    {
        float totalMass = massLeft + massRight;

        if (leftGrabbed && rightGrabbed)
        {
            velocity = 0f;
            leftLength = slotLeft.position.y - hookLeft.position.y;
            rightLength = slotRight.position.y - hookRight.position.y;
        }
        else if (leftGrabbed)
        {
            leftLength = Vector3.Distance(slotLeft.position, hookLeft.position);
            leftLength = Mathf.Clamp(leftLength, 0.05f, totalRopeLength - 0.05f);
            rightLength = totalRopeLength - leftLength;
            velocity = 0f;
            if (hookRight != null)
                hookRight.position = new Vector3(slotRight.position.x, slotRight.position.y - rightLength, slotRight.position.z);
        }
        else if (rightGrabbed)
        {
            rightLength = Vector3.Distance(slotRight.position, hookRight.position);
            rightLength = Mathf.Clamp(rightLength, 0.05f, totalRopeLength - 0.05f);
            leftLength = totalRopeLength - rightLength;
            velocity = 0f;
            if (hookLeft != null)
                hookLeft.position = new Vector3(slotLeft.position.x, slotLeft.position.y - leftLength, slotLeft.position.z);
        }
        else
        {
            if (totalMass == 0f)
            {
                leftLength = Mathf.MoveTowards(leftLength, totalRopeLength * 0.5f, 0.5f * Time.fixedDeltaTime);
                rightLength = totalRopeLength - leftLength;
            }
            else
            {
                float acceleration = (massLeft - massRight) * g / totalMass;
                velocity += acceleration * Time.fixedDeltaTime;
                velocity = Mathf.Clamp(velocity, -2f, 2f);
                leftLength += velocity * Time.fixedDeltaTime;
                rightLength = totalRopeLength - leftLength;
            }

            leftLength = Mathf.Clamp(leftLength, 0.05f, totalRopeLength - 0.05f);
            rightLength = Mathf.Clamp(rightLength, 0.05f, totalRopeLength - 0.05f);

            if (hookLeft != null)
                hookLeft.position = new Vector3(slotLeft.position.x, slotLeft.position.y - leftLength, slotLeft.position.z);
            if (hookRight != null)
                hookRight.position = new Vector3(slotRight.position.x, slotRight.position.y - rightLength, slotRight.position.z);
        }

        if (weightChainLeft != null && hookLeft != null) SetChainPosition(weightChainLeft, hookLeft.position);
        if (weightChainRight != null && hookRight != null) SetChainPosition(weightChainRight, hookRight.position);
    }

    private void UpdateMovablePulley(float MA, bool leftGrabbed, bool rightGrabbed)
    {
        if (fixedPulleySlotRef == null) return;

        float loadMass = weightChainLoad != null ? GetChainMass(weightChainLoad) : 0f;
        float forceMass = weightChainForce != null ? GetChainMass(weightChainForce) : 0f;
        bool freeEndGrabbed = leftGrabbed || rightGrabbed;

        float fixedY = fixedPulleySlotRef.position.y;

        if (freeEndGrabbed)
        {
            // Player pulling free end - enforce rope constraint
            forceY = freeEndHook.position.y;
            float d2 = Mathf.Abs(fixedY - forceY);
            float d1 = (totalRopeLengthMA2 - d2) / 2f;
            d1 = Mathf.Max(d1, 0.01f);
            loadY = fixedY - d1;
            velocity = 0f;
        }
        else if (loadMass == 0f && forceMass == 0f)
        {
            velocity = 0f;
            return;
        }
        else
        {
            float netForce = (loadMass - forceMass * MA) * g;
            float totalInertia = loadMass + forceMass * MA * MA;
            float acceleration = netForce / totalInertia;

            velocity += acceleration * Time.fixedDeltaTime;
            velocity = Mathf.Clamp(velocity, -2f, 2f);

            loadY -= velocity * Time.fixedDeltaTime;

            // Enforce rope constraint: d1*2 + d2 = L
            float d1 = Mathf.Abs(fixedY - loadY);
            float d2 = totalRopeLengthMA2 - d1 * 2f;

            float minD1 = 0.01f;
            float minD2 = 0.01f;

            d1 = Mathf.Clamp(d1, minD1, (totalRopeLengthMA2 - minD2) / 2f);
            d2 = totalRopeLengthMA2 - d1 * 2f;
            d2 = Mathf.Max(d2, minD2);
            d1 = (totalRopeLengthMA2 - d2) / 2f;

            if (d2 <= minD2 || d1 <= minD1) velocity = 0f;

            loadY = fixedY - d1;
            forceY = fixedY - d2;
        }

        // Move entire movable pulley (parent of movablePulleyBottom)
        Transform movablePulley = movablePulleyBottom?.parent;
        if (movablePulley != null)
            movablePulley.position = new Vector3(
                movablePulley.position.x,
                loadY,
                movablePulley.position.z);

        if (freeEndHook != null)
            freeEndHook.position = new Vector3(
                freeEndHook.position.x,
                forceY,
                freeEndHook.position.z);

        if (weightChainLoad != null && movablePulleyBottom != null)
            SetChainPosition(weightChainLoad, movablePulleyBottom.position);
        if (weightChainForce != null && freeEndHook != null)
            SetChainPosition(weightChainForce, freeEndHook.position);
    }

    // Called by PulleySystem when rope is updated
    public void SetFreeEndHook(Transform hook)
    {
        freeEndHook = hook;
        if (hook != null)
            forceY = hook.position.y;
        Debug.Log("Free end hook set to: " + hook?.name);
    }

    // Called by PulleySystem to set fixed pulley reference and initialize rope length
    public void SetFixedPulleyRef(Transform fixedSlot)
    {
        fixedPulleySlotRef = fixedSlot;
        if (fixedSlot != null && movablePulleyBottom != null && freeEndHook != null)
        {
            Transform movablePulley = movablePulleyBottom.parent;
            float pulleyY = movablePulley != null ? movablePulley.position.y : movablePulleyBottom.position.y;
            float d1 = Mathf.Abs(fixedSlot.position.y - pulleyY);
            float d2 = Mathf.Abs(fixedSlot.position.y - freeEndHook.position.y);
            totalRopeLengthMA2 = d1 * 2f + d2;
            loadY = pulleyY;
            forceY = freeEndHook.position.y;
            velocity = 0f;
            Debug.Log("Rope length MA2 initialized: " + totalRopeLengthMA2);
        }
    }

    float GetChainMass(GameObject topWeight)
    {
        float total = 0f;
        foreach (Rigidbody rb in topWeight.GetComponentsInChildren<Rigidbody>())
            total += rb.mass;
        return total;
    }

    void SetChainPosition(GameObject topWeight, Vector3 position)
    {
        topWeight.transform.position = position;
        int childIndex = 0;
        foreach (Transform child in topWeight.transform)
        {
            if (child.GetComponent<Rigidbody>() != null)
            {
                SetChainPosition(child.gameObject, position + Vector3.down * 0.05f * (childIndex + 1));
                childIndex++;
            }
        }
    }
}