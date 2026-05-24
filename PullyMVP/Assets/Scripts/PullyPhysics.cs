using UnityEngine;

public class PulleyPhysics : MonoBehaviour
{
    public GameObject weightChainLeft;
    public GameObject weightChainRight;
    public Transform slotLeft;
    public Transform slotRight;
    public float totalRopeLength = 2f;
    public Transform hookLeft;
    public Transform hookRight;
    public float leftLength;
    public float rightLength;
    public float velocity = 0f;

    // Exposed for RopeGrab haptic feedback
    public float massLeft = 0f;
    public float massRight = 0f;

    void Start()
    {
        leftLength = totalRopeLength * 0.5f;
        rightLength = totalRopeLength * 0.5f;
        velocity = 0f;
    }

    void FixedUpdate()
    {
        RopeGrab ropeGrabLeft = hookLeft != null ? hookLeft.GetComponent<RopeGrab>() : null;
        RopeGrab ropeGrabRight = hookRight != null ? hookRight.GetComponent<RopeGrab>() : null;

        bool leftGrabbed = ropeGrabLeft != null && ropeGrabLeft.isGrabbed;
        bool rightGrabbed = ropeGrabRight != null && ropeGrabRight.isGrabbed;

        // Update mass fields every frame
        massLeft = weightChainLeft != null ? GetChainMass(weightChainLeft) : 0f;
        massRight = weightChainRight != null ? GetChainMass(weightChainRight) : 0f;

        float g = 9.81f;
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

            float minY = 0.05f;
            if (slotLeft.position.y - leftLength < minY)
            {
                leftLength = slotLeft.position.y - minY;
                rightLength = totalRopeLength - leftLength;
                velocity = 0f;
            }
            if (slotRight.position.y - rightLength < minY)
            {
                rightLength = slotRight.position.y - minY;
                leftLength = totalRopeLength - rightLength;
                velocity = 0f;
            }

            leftLength = Mathf.Clamp(leftLength, 0.05f, totalRopeLength - 0.05f);
            rightLength = totalRopeLength - leftLength;

            if (hookLeft != null)
                hookLeft.position = new Vector3(slotLeft.position.x, slotLeft.position.y - leftLength, slotLeft.position.z);
            if (hookRight != null)
                hookRight.position = new Vector3(slotRight.position.x, slotRight.position.y - rightLength, slotRight.position.z);
        }

        if (weightChainLeft != null && hookLeft != null)
            SetChainPosition(weightChainLeft, hookLeft.position);
        if (weightChainRight != null && hookRight != null)
            SetChainPosition(weightChainRight, hookRight.position);
    }

    float GetChainMass(GameObject topWeight)
    {
        float total = 0f;
        Rigidbody[] allRigidbodies = topWeight.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in allRigidbodies)
        {
            total += rb.mass;
        }
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