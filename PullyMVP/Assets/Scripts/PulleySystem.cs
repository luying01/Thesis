using UnityEngine;
using System.Collections.Generic;

public class PulleySystem : MonoBehaviour
{
    [Header("Slots - Fixed Pulleys")]
    public PulleySlot[] fixedPulleySlots;

    [Header("References")]
    public Transform arm;
    public Transform ropeEndLeft;
    public Transform ropeEndRight;

    [Header("Read Only")]
    public int ropeSegments = 1;
    public float mechanicalAdvantage = 1f;

    private RopeSystem ropeSystem;
    private Transform lockedMovablePulley = null;
    private PulleyPhysics pulleyPhysics;
    private PulleySlot cachedFixedSlot = null;
    private bool movableIsRight = false;

    private void Awake()
    {
        ropeSystem = FindObjectOfType<RopeSystem>();
        pulleyPhysics = FindObjectOfType<PulleyPhysics>();
    }

    private void Update()
    {
        if (lockedMovablePulley != null && cachedFixedSlot != null)
            UpdateRopePath();
    }

    public void OnPulleyPlaced(PulleySlot slot)
    {
        RecalculateSystem();
    }

    public void OnPulleyRemoved(PulleySlot slot)
    {
        cachedFixedSlot = null;
        RecalculateSystem();
    }

    public void OnMovablePulleyLocked(Transform pulleyTransform)
    {
        lockedMovablePulley = pulleyTransform;
        RecalculateSystem();
    }

    public void OnMovablePulleyUnlocked()
    {
        lockedMovablePulley = null;
        cachedFixedSlot = null;
        RecalculateSystem();
    }

    private void RecalculateSystem()
    {
        int fixedCount = CountOccupiedFixed();
        int movableCount = lockedMovablePulley != null ? 1 : 0;

        ropeSegments = movableCount * 2 + 1;
        mechanicalAdvantage = Mathf.Max(1f, movableCount * 2f);

        Debug.Log($"Fixed: {fixedCount}, Movable: {movableCount}, " +
                  $"Segments: {ropeSegments}, MA: {mechanicalAdvantage}");

        // Cache fixed slot and side direction
        cachedFixedSlot = null;
        foreach (var slot in fixedPulleySlots)
        {
            if (slot != null && slot.isOccupied)
            {
                cachedFixedSlot = slot;
                break;
            }
        }

        if (cachedFixedSlot != null && lockedMovablePulley != null)
            movableIsRight = lockedMovablePulley.position.z < cachedFixedSlot.transform.position.z;

        NotifyPulleyPhysics();
        UpdateRopePath();
    }

    private int CountOccupiedFixed()
    {
        int count = 0;
        foreach (var slot in fixedPulleySlots)
            if (slot != null && slot.isOccupied) count++;
        return count;
    }

    private void UpdateRopePath()
    {
        if (ropeSystem == null) return;
        if (lockedMovablePulley == null) return;
        if (cachedFixedSlot == null) return;

        Transform fixedSlotL = FindChildByName(cachedFixedSlot.snappedPulleyObject.transform, "RopeSlot_L");
        Transform fixedSlotR = FindChildByName(cachedFixedSlot.snappedPulleyObject.transform, "RopeSlot_R");
        Transform fixedTop = FindChildByName(cachedFixedSlot.snappedPulleyObject.transform, "S_Top");
        Transform movableSlotL = FindChildByName(lockedMovablePulley, "RopeSlot_L");
        Transform movableSlotR = FindChildByName(lockedMovablePulley, "RopeSlot_R");

        if (fixedSlotL == null || fixedSlotR == null ||
            movableSlotL == null || movableSlotR == null || fixedTop == null) return;

        List<Vector3> waypoints = new List<Vector3>();
        Transform ropeEnd;

        if (movableIsRight)
        {
            Vector3 deadEnd = new Vector3(
                movableSlotR.position.x,
                fixedTop.position.y,
                movableSlotR.position.z
            );
            waypoints.Add(deadEnd);
            waypoints.Add(movableSlotR.position);
            waypoints.Add(movableSlotL.position);
            waypoints.Add(fixedSlotR.position);
            waypoints.Add(fixedSlotL.position);

            ropeEnd = ropeEndLeft;
            Vector3 freeEnd = new Vector3(
                fixedSlotL.position.x,
                ropeEnd.position.y,
                fixedSlotL.position.z
            );
            waypoints.Add(freeEnd);
            waypoints.Add(ropeEnd.position);
        }
        else
        {
            Vector3 deadEnd = new Vector3(
                movableSlotL.position.x,
                fixedTop.position.y,
                movableSlotL.position.z
            );
            waypoints.Add(deadEnd);
            waypoints.Add(movableSlotL.position);
            waypoints.Add(movableSlotR.position);
            waypoints.Add(fixedSlotL.position);
            waypoints.Add(fixedSlotR.position);

            ropeEnd = ropeEndRight;
            Vector3 freeEnd = new Vector3(
                fixedSlotR.position.x,
                ropeEnd.position.y,
                fixedSlotR.position.z
            );
            waypoints.Add(freeEnd);
            waypoints.Add(ropeEnd.position);
        }

        ropeSystem.SetWaypoints(waypoints.ToArray());
    }

    private void NotifyPulleyPhysics()
    {
        if (cachedFixedSlot == null) return;

        Transform fixedSlotL = FindChildByName(cachedFixedSlot.snappedPulleyObject.transform, "RopeSlot_L");
        Transform fixedSlotR = FindChildByName(cachedFixedSlot.snappedPulleyObject.transform, "RopeSlot_R");

        Transform ropeEnd = movableIsRight ? ropeEndLeft : ropeEndRight;
        Transform fixedRef = movableIsRight ? fixedSlotL : fixedSlotR;

        pulleyPhysics?.SetFreeEndHook(ropeEnd);
        pulleyPhysics?.SetFixedPulleyRef(fixedRef);
    }

    private Transform FindChildByName(Transform parent, string partialName)
    {
        Transform[] allChildren = parent.GetComponentsInChildren<Transform>();
        foreach (var t in allChildren)
            if (t.name.Contains(partialName))
                return t;
        return null;
    }

    public PulleySlot GetNearestOccupiedFixedSlot(Vector3 position)
    {
        PulleySlot nearest = null;
        float minDist = float.MaxValue;

        foreach (var slot in fixedPulleySlots)
        {
            if (slot != null && slot.isOccupied)
            {
                float dist = Vector3.Distance(position, slot.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = slot;
                }
            }
        }
        return nearest;
    }

    public float GetMechanicalAdvantage()
    {
        return mechanicalAdvantage;
    }
}