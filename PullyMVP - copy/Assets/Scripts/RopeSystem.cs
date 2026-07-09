using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeSystem : MonoBehaviour
{
    public Transform hookLeft;
    public Transform hookRight;
    public Transform slotLeft;
    public Transform slotRight;

    private LineRenderer lr;
    private Vector3[] customWaypoints = null;
    private bool useCustomWaypoints = false;

    void Start()
    {
        lr = GetComponent<LineRenderer>();
        lr.startWidth = 0.005f;
        lr.endWidth = 0.005f;
    }

    void Update()
    {
        if (useCustomWaypoints && customWaypoints != null)
        {
            lr.positionCount = customWaypoints.Length;
            for (int i = 0; i < customWaypoints.Length; i++)
                lr.SetPosition(i, customWaypoints[i]);
        }
        else
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

    public void SetWaypoints(Vector3[] waypoints)
    {
        customWaypoints = waypoints;
        useCustomWaypoints = (waypoints != null && waypoints.Length >= 2);
        Debug.Log("SetWaypoints called, count=" + waypoints.Length +
                  " useCustom=" + useCustomWaypoints +
                  " first=" + waypoints[0] + " last=" + waypoints[waypoints.Length - 1]);
    }
}
