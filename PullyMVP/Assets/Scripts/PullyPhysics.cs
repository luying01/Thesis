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

    void Start()
    {
        leftLength = totalRopeLength * 0.5f;
        rightLength = totalRopeLength * 0.5f;
        velocity = 0f;
    }

    void FixedUpdate()
    {
        float massLeft = weightChainLeft != null ? GetChainMass(weightChainLeft) : 0f;
        float massRight = weightChainRight != null ? GetChainMass(weightChainRight) : 0f;
        float g = 9.81f;
        float totalMass = massLeft + massRight;

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

        // 底部限制
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

        // 先更新Hook位置
        if (hookLeft != null)
            hookLeft.position = new Vector3(slotLeft.position.x, slotLeft.position.y - leftLength, slotLeft.position.z);
        if (hookRight != null)
            hookRight.position = new Vector3(slotRight.position.x, slotRight.position.y - rightLength, slotRight.position.z);

        // 再把砝码放到Hook位置
        if (weightChainLeft != null && hookLeft != null)
            SetChainPosition(weightChainLeft, hookLeft.position);
        if (weightChainRight != null && hookRight != null)
            SetChainPosition(weightChainRight, hookRight.position);
    }

    float GetChainMass(GameObject topWeight)
    {
        float total = 0f;
        Rigidbody rb = topWeight.GetComponent<Rigidbody>();
        if (rb != null) total += rb.mass;
        foreach (Transform child in topWeight.transform)
        {
            if (child.GetComponent<Rigidbody>() != null)
                total += GetChainMass(child.gameObject);
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