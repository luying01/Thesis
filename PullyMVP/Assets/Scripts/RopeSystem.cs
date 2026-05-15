using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeSystem : MonoBehaviour
{
    public Transform hookLeft;       // ×ó²àÉþ×Ó¶Ëµã
    public Transform hookRight;      // ÓÒ²àÉþ×Ó¶Ëµã
    public Transform slotLeft;       // »¬ÂÖ×ó²Û
    public Transform slotRight;      // »¬ÂÖÓÒ²Û

    LineRenderer lr;

    void Start()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 4;
        lr.startWidth = 0.005f;
        lr.endWidth = 0.005f;
    }

    void Update()
    {
        if (hookLeft == null || hookRight == null ||
            slotLeft == null || slotRight == null) return;

        Vector3 pulleyCenter = (slotLeft.position + slotRight.position) * 0.5f;
        float px = pulleyCenter.x;

        Vector3 exitLeft = new Vector3(px, pulleyCenter.y, slotLeft.position.z);
        Vector3 exitRight = new Vector3(px, pulleyCenter.y, slotRight.position.z);

        Vector3 topLeft = exitLeft + Vector3.up * 0.02f + new Vector3(0, 0, -0.01f);
        Vector3 topRight = exitRight + Vector3.up * 0.02f + new Vector3(0, 0, 0.01f);

        lr.positionCount = 6;
        lr.SetPosition(0, hookLeft.position);
        lr.SetPosition(1, exitLeft);
        lr.SetPosition(2, topLeft);
        lr.SetPosition(3, topRight);
        lr.SetPosition(4, exitRight);
        lr.SetPosition(5, hookRight.position);
    }
}
