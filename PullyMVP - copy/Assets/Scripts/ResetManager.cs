using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ResetManager : MonoBehaviour
{
    [Header("Objects to Reset")]
    public GameObject[] objectsToReset;

    private Vector3[] initialPositions;
    private Quaternion[] initialRotations;
    private Vector3[] initialScales;
    private Transform[] initialParents;

    void Start()
    {
        initialPositions = new Vector3[objectsToReset.Length];
        initialRotations = new Quaternion[objectsToReset.Length];
        initialScales = new Vector3[objectsToReset.Length];
        initialParents = new Transform[objectsToReset.Length];

        for (int i = 0; i < objectsToReset.Length; i++)
        {
            if (objectsToReset[i] != null)
            {
                initialPositions[i] = objectsToReset[i].transform.position;
                initialRotations[i] = objectsToReset[i].transform.rotation;
                initialScales[i] = objectsToReset[i].transform.localScale;
                initialParents[i] = objectsToReset[i].transform.parent;
            }
        }
        Debug.Log("ResetManager: Recorded initial positions for " + objectsToReset.Length + " objects.");
    }

    public void ResetAll()
    {
        // ���� Step 1: Detach all weights ��������������������������������������������������������������������������
        WeightSnap[] allWeights = FindObjectsOfType<WeightSnap>();
        foreach (WeightSnap weight in allWeights)
        {
            weight.isSnapped = false;
            weight.transform.SetParent(null);
        }

        // ���� Step 2: Release all pulleys from slots ��������������������������������������������������
        PulleyObject[] allPulleys = FindObjectsOfType<PulleyObject>();
        foreach (PulleyObject pulley in allPulleys)
        {
            var currentSlotField = typeof(PulleyObject).GetField("currentSlot",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (currentSlotField != null)
                currentSlotField.SetValue(pulley, null);
            pulley.ShowSnapHighlight(false);
        }

        // ���� Step 3: Clear all PulleySlot states ��������������������������������������������������������
        PulleySlot[] allSlots = FindObjectsOfType<PulleySlot>();
        foreach (PulleySlot slot in allSlots)
        {
            slot.isOccupied = false;
            slot.snappedPulleyObject = null;
        }

        // ���� Step 3.5: Reset MovablePulleyLock state ������������������������������������������������
        MovablePulleyLock[] allLocks = FindObjectsOfType<MovablePulleyLock>();
        foreach (MovablePulleyLock mpl in allLocks)
        {
            if (mpl.isLocked)
            {
                mpl.isLocked = false;
                mpl.pulleySystem?.OnMovablePulleyUnlocked();
            }
        }

        // ── Step 4: Reset PulleySystem internal state ──────────────────────────
        PulleySystem pulleySystem = FindObjectOfType<PulleySystem>();
        if (pulleySystem != null)
        {
            pulleySystem.ResetToDefaultState();
        }

        // ���� Step 5: Reset PulleyPhysics state ����������������������������������������������������������
        PulleyPhysics pulleyPhysics = FindObjectOfType<PulleyPhysics>();
        if (pulleyPhysics != null)
        {
            pulleyPhysics.weightChainLeft = null;
            pulleyPhysics.weightChainRight = null;
            pulleyPhysics.weightChainLoad = null;
            pulleyPhysics.weightChainForce = null;
            pulleyPhysics.freeEndHook = null;
            pulleyPhysics.velocity = 0f;
            pulleyPhysics.leftLength = pulleyPhysics.totalRopeLength * 0.5f;
            pulleyPhysics.rightLength = pulleyPhysics.totalRopeLength * 0.5f;

            // Reset tracking and trajectory lines
            pulleyPhysics.ResetTracking();

            var loadYField = typeof(PulleyPhysics).GetField("loadY",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var forceYField = typeof(PulleyPhysics).GetField("forceY",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var fixedRefField = typeof(PulleyPhysics).GetField("fixedPulleySlotRef",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (loadYField != null) loadYField.SetValue(pulleyPhysics, 0f);
            if (forceYField != null) forceYField.SetValue(pulleyPhysics, 0f);
            if (fixedRefField != null) fixedRefField.SetValue(pulleyPhysics, null);
        }

        // ���� Step 6: Reset RopeSystem ������������������������������������������������������������������������������
        RopeSystem ropeSystem = FindObjectOfType<RopeSystem>();
        if (ropeSystem != null)
        {
            var customWaypointsField = typeof(RopeSystem).GetField("customWaypoints",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var useCustomField = typeof(RopeSystem).GetField("useCustomWaypoints",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (customWaypointsField != null) customWaypointsField.SetValue(ropeSystem, null);
            if (useCustomField != null) useCustomField.SetValue(ropeSystem, false);
        }

        // ���� Step 7: Reset RopeGrab states ������������������������������������������������������������������
        RopeGrab[] allRopeGrabs = FindObjectsOfType<RopeGrab>();
        foreach (RopeGrab ropeGrab in allRopeGrabs)
            ropeGrab.isGrabbed = false;

        // ���� Step 8: Restore all objects to initial transform ������������������������������
        for (int i = 0; i < objectsToReset.Length; i++)
        {
            if (objectsToReset[i] != null)
            {
                objectsToReset[i].transform.SetParent(initialParents[i]);
                objectsToReset[i].transform.position = initialPositions[i];
                objectsToReset[i].transform.rotation = initialRotations[i];
                objectsToReset[i].transform.localScale = initialScales[i];

                Rigidbody rb = objectsToReset[i].GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        // ── Step 9: Re-apply auto-snap for slots configured to hold a pulley at start ──
        PulleySlot[] slotsForResnap = FindObjectsOfType<PulleySlot>();
        foreach (PulleySlot slot in slotsForResnap)
        {
            slot.ApplyAutoSnapIfConfigured();
        }

        Debug.Log("ResetManager: Reset complete.");
    }
    public Vector3 GetInitialPosition(GameObject obj)
    {
        for (int i = 0; i < objectsToReset.Length; i++)
            if (objectsToReset[i] == obj) return initialPositions[i];
        return obj.transform.position;
    }
}