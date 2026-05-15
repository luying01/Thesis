using UnityEngine;

public class PulleyPhysics : MonoBehaviour
{
    public GameObject weightChainLeft;
    public GameObject weightChainRight;
    public Transform slotLeft;
    public Transform slotRight;
    public float totalRopeLength = 2f; // 绳子总长度固定
    public Transform hookLeft;
    public Transform hookRight;

    float leftLength;  // 左侧绳子当前长度
    float rightLength; // 右侧绳子当前长度
    float velocity = 0f;

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
        Debug.Log("left: " + leftLength + " right: " + rightLength);

        if (totalMass == 0f)
        {
            // 没有砝码，两侧自然下垂到绳子中点
            leftLength = Mathf.MoveTowards(leftLength, totalRopeLength * 0.5f, 0.5f * Time.fixedDeltaTime);
            rightLength = totalRopeLength - leftLength;
        }
        else
        {
            // 阿特伍德机公式
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

        if (weightChainLeft != null && slotLeft != null)
            SetChainPosition(weightChainLeft, slotLeft.position + Vector3.down * leftLength);
        if (weightChainRight != null && slotRight != null)
            SetChainPosition(weightChainRight, slotRight.position + Vector3.down * rightLength);

        Vector3 pulleyCenter = (slotLeft.position + slotRight.position) * 0.5f;
        if (hookLeft != null)
            hookLeft.position = new Vector3(pulleyCenter.x, pulleyCenter.y - leftLength, slotLeft.position.z);
        if (hookRight != null)
            hookRight.position = new Vector3(pulleyCenter.x, pulleyCenter.y - rightLength, slotRight.position.z);
    }

    float GetChainMass(GameObject topWeight)
    {
        float total = 0f;
        Rigidbody rb = topWeight.GetComponent<Rigidbody>();
        if (rb != null) total += rb.mass;

        // 遍历所有直接子物体，找下一个有Rigidbody的砝码
        foreach (Transform child in topWeight.transform)
        {
            if (child.GetComponent<Rigidbody>() != null)
            {
                total += GetChainMass(child.gameObject);
            }
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