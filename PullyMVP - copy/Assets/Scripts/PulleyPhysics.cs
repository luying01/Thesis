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

    [Tooltip("Closest the movable pulley may come to the fixed pulley.")]
    public float minD1 = 0.01f;

    [Tooltip("Longest rope the student may create by hanging the movable pulley " +
             "low. The free end's lowest reachable point is roughly the fixed " +
             "pulley's height minus this, so it also decides whether the free " +
             "end can run below the table.")]
    public float maxRopeLengthMA2 = 0.6f;

    [Header("Read Only")]
    public float leftLength;
    public float rightLength;
    public float velocity = 0f;
    public float massLeft = 0f;
    public float massRight = 0f;

    [Header("Read Only - For ConceptualAid")]
    public float acceleration = 0f;
    public float tensionForce = 0f;
    public float distanceHookL = 0f;
    public float distanceHookR = 0f;
    public float distanceMovablePulley = 0f;

    [Header("Trajectory Lines")]
    public LineRenderer lineHookL;
    public LineRenderer lineHookR;
    public LineRenderer lineMovablePulley;

    // Private tracking variables
    private float previousVelocity = 0f;
    private Vector3 startPositionHookL;
    private Vector3 startPositionHookR;
    private Vector3 startPositionMovablePulley;
    private bool trackingHookL = false;
    private bool trackingHookR = false;
    private bool trackingMovablePulley = false;

    // Load-side (movable pulley) trigger-drag state
    private bool loadGrabbed = false;

    private float g = 9.81f;
    private float loadY;
    private float forceY;
    private Transform fixedPulleySlotRef;

    // ── Unity Lifecycle ───────────────────────────────────────────

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

        InitLine(lineHookL);
        InitLine(lineHookR);
        InitLine(lineMovablePulley);

        UpdateDeadEndHookVisibility();
    }

    void FixedUpdate()
    {
        previousVelocity = velocity;

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

        acceleration = (velocity - previousVelocity) / Time.fixedDeltaTime;
    }

    // ── Atwood ───────────────────────────────────────────────────

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
                float acceleration_raw = (massLeft - massRight) * g / totalMass;
                velocity += acceleration_raw * Time.fixedDeltaTime;
                velocity = Mathf.Clamp(velocity, -2f, 2f);
                tensionForce = massRight * (g + acceleration_raw);
                leftLength += velocity * Time.fixedDeltaTime;
                rightLength = totalRopeLength - leftLength;
            }

            leftLength = Mathf.Clamp(leftLength, 0.05f, totalRopeLength - 0.05f);
            rightLength = Mathf.Clamp(rightLength, 0.05f, totalRopeLength - 0.05f);

            // A hook that has reached the end of its travel is physically at
            // rest. Without this, velocity stays pinned at the clamp value
            // forever: the positions freeze but the solver keeps reporting
            // motion, so anything waiting for the system to settle waits
            // indefinitely. The movable-pulley branch already does this.
            float minLen = 0.05f;
            float maxLen = totalRopeLength - 0.05f;
            if (leftLength <= minLen || leftLength >= maxLen ||
                rightLength <= minLen || rightLength >= maxLen)
            {
                velocity = 0f;
            }

            if (hookLeft != null)
                hookLeft.position = new Vector3(slotLeft.position.x, slotLeft.position.y - leftLength, slotLeft.position.z);
            if (hookRight != null)
                hookRight.position = new Vector3(slotRight.position.x, slotRight.position.y - rightLength, slotRight.position.z);
        }

        if (trackingHookL && hookLeft != null)
        {
            distanceHookL = Vector3.Distance(hookLeft.position, startPositionHookL);
            AppendLine(lineHookL, hookLeft.position);
        }
        if (trackingHookR && hookRight != null)
        {
            distanceHookR = Vector3.Distance(hookRight.position, startPositionHookR);
            AppendLine(lineHookR, hookRight.position);
        }

        if (weightChainLeft != null && hookLeft != null) SetChainPosition(weightChainLeft, hookLeft.position);
        if (weightChainRight != null && hookRight != null) SetChainPosition(weightChainRight, hookRight.position);
    }

    // ── Movable Pulley ────────────────────────────────────────────

    private void UpdateMovablePulley(float MA, bool leftGrabbed, bool rightGrabbed)
    {
        if (fixedPulleySlotRef == null) return;

        float loadMass = weightChainLoad != null ? GetChainMass(weightChainLoad) : 0f;
        float forceMass = weightChainForce != null ? GetChainMass(weightChainForce) : 0f;
        bool freeEndGrabbed = leftGrabbed || rightGrabbed;

        float fixedY = fixedPulleySlotRef.position.y;

        if (freeEndGrabbed)
        {
            float d2 = Vector3.Distance(fixedPulleySlotRef.position, freeEndHook.position);
            float d1 = (totalRopeLengthMA2 - d2) / 2f;
            d1 = Mathf.Max(d1, minD1);
            loadY = fixedY - d1;
            forceY = freeEndHook.position.y;
            velocity = 0f;

            float loadMassGrabbed = weightChainLoad != null ? GetChainMass(weightChainLoad) : 0f;
            tensionForce = loadMassGrabbed * g / MA;
        }
        else if (loadGrabbed)
        {
            // loadY / forceY / movable pulley position were already updated
            // directly by DragLoadTo(), called from MovablePulleyLock's trigger-drag.
            float loadMassGrabbed = weightChainLoad != null ? GetChainMass(weightChainLoad) : 0f;
            tensionForce = loadMassGrabbed * g / MA;
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
            float acceleration_raw = netForce / totalInertia;

            tensionForce = (loadMass * g - loadMass * acceleration_raw) / MA;

            velocity += acceleration_raw * Time.fixedDeltaTime;
            velocity = Mathf.Clamp(velocity, -2f, 2f);

            loadY -= velocity * Time.fixedDeltaTime;

            float d1 = Mathf.Abs(fixedY - loadY);
            float d2 = totalRopeLengthMA2 - d1 * 2f;
            float minD2 = 0.01f;
            float maxD1 = (totalRopeLengthMA2 - minD2) / 2f;

            // Test the raw value, before clamping, and block only motion that
            // pushes further into the limit.
            //
            // loadY -= velocity, so a NEGATIVE velocity raises the pulley and
            // shrinks d1. The signs were the wrong way round, which is why the
            // check never fired and velocity sat pinned at the -2 cap.
            bool pushingIntoLimit =
                (d1 <= minD1 && velocity < 0f) ||
                (d1 >= maxD1 && velocity > 0f);

            d1 = Mathf.Clamp(d1, minD1, maxD1);
            d2 = totalRopeLengthMA2 - d1 * 2f;
            d2 = Mathf.Max(d2, minD2);
            d1 = (totalRopeLengthMA2 - d2) / 2f;

            if (pushingIntoLimit) velocity = 0f;

            loadY = fixedY - d1;
            forceY = fixedY - d2;
        }

        Transform movablePulley = movablePulleyBottom?.parent;
        if (movablePulley != null && !loadGrabbed)
            movablePulley.position = new Vector3(
                movablePulley.position.x, loadY, movablePulley.position.z);

        // On release the hook returns to hanging directly under its own rope
        // slot, and only its height is left to the physics - the same thing the
        // Atwood branch already does. Keeping the release X/Z here instead left
        // the hook oscillating wherever the hand happened to let go, so the same
        // action gave different feedback on a single fixed pulley and on a
        // movable one, which is noise the study does not want.
        if (freeEndHook != null && !freeEndGrabbed)
        {
            Transform freeSlot = (freeEndHook == hookLeft) ? slotLeft : slotRight;
            Vector3 restPosition = freeSlot != null
                ? new Vector3(freeSlot.position.x, forceY, freeSlot.position.z)
                : new Vector3(freeEndHook.position.x, forceY, freeEndHook.position.z);

            freeEndHook.position = restPosition;
        }
        if (trackingMovablePulley && movablePulleyBottom != null)
        {
            distanceMovablePulley = Vector3.Distance(movablePulleyBottom.position, startPositionMovablePulley);
            AppendLine(lineMovablePulley, movablePulleyBottom.position);
        }

        if (freeEndHook != null)
        {
            bool freeEndIsLeft = (freeEndHook == hookLeft);

            if (freeEndIsLeft && trackingHookL)
            {
                distanceHookL = Vector3.Distance(freeEndHook.position, startPositionHookL);
                AppendLine(lineHookL, freeEndHook.position);
            }
            else if (!freeEndIsLeft && trackingHookR)
            {
                distanceHookR = Vector3.Distance(freeEndHook.position, startPositionHookR);
                AppendLine(lineHookR, freeEndHook.position);
            }
        }

        if (weightChainLoad != null && movablePulleyBottom != null)
            SetChainPosition(weightChainLoad, movablePulleyBottom.position);
        if (weightChainForce != null && freeEndHook != null)
            SetChainPosition(weightChainForce, freeEndHook.position);
    }

    // ── Trajectory Lines ──────────────────────────────────────────

    private void InitLine(LineRenderer lr)
    {
        if (lr == null) return;
        lr.positionCount = 0;
        lr.startWidth = 0.003f;
        lr.endWidth = 0.003f;
        lr.useWorldSpace = true;
        lr.enabled = false;
    }

    private void AppendLine(LineRenderer lr, Vector3 currentPosition)
    {
        if (lr == null || !lr.enabled) return;
        lr.positionCount = 2;
        lr.SetPosition(1, currentPosition);
    }

    // ── Public: Start Tracking ────────────────────────────────────

    public void StartLoadTracking(string endpoint, Vector3 startPos)
    {
        switch (endpoint)
        {
            case "HookL":
                startPositionHookL = startPos;
                trackingHookL = true;
                distanceHookL = 0f;
                if (lineHookL != null)
                {
                    lineHookL.positionCount = 2;
                    lineHookL.SetPosition(0, startPos);
                    lineHookL.SetPosition(1, startPos);
                }
                break;
            case "HookR":
                startPositionHookR = startPos;
                trackingHookR = true;
                distanceHookR = 0f;
                if (lineHookR != null)
                {
                    lineHookR.positionCount = 2;
                    lineHookR.SetPosition(0, startPos);
                    lineHookR.SetPosition(1, startPos);
                }
                break;
            case "MovablePulley":
                startPositionMovablePulley = startPos;
                trackingMovablePulley = true;
                distanceMovablePulley = 0f;
                if (lineMovablePulley != null)
                {
                    lineMovablePulley.positionCount = 2;
                    lineMovablePulley.SetPosition(0, startPos);
                    lineMovablePulley.SetPosition(1, startPos);
                }
                break;
        }
        Debug.Log($"[PulleyPhysics] Started tracking {endpoint}");
    }

    // ── Public Getters for ConceptualAidManager ───────────────────

    public bool IsTrackingHookL() => trackingHookL;
    public bool IsTrackingHookR() => trackingHookR;
    public bool IsTrackingMovablePulley() => trackingMovablePulley;

    public Vector3 GetStartPositionHookL() => startPositionHookL;
    public Vector3 GetStartPositionHookR() => startPositionHookR;
    public Vector3 GetStartPositionMovablePulley() => startPositionMovablePulley;

    public bool IsMovablePulleyConfig() =>
        pulleySystem != null && pulleySystem.GetMechanicalAdvantage() > 1f;

    public float GetLoadMass() =>
        weightChainLoad != null ? GetChainMass(weightChainLoad) : 0f;

    public float GetForceMass() =>
    weightChainForce != null ? GetChainMass(weightChainForce) : 0f;

    // ── Public: Reset ─────────────────────────────────────────────

    public void ResetTracking()
    {
        trackingHookL = false;
        trackingHookR = false;
        trackingMovablePulley = false;
        distanceHookL = 0f;
        distanceHookR = 0f;
        distanceMovablePulley = 0f;
        acceleration = 0f;
        tensionForce = 0f;
        velocity = 0f;
        loadGrabbed = false;

        if (lineHookL != null) lineHookL.positionCount = 0;
        if (lineHookR != null) lineHookR.positionCount = 0;
        if (lineMovablePulley != null) lineMovablePulley.positionCount = 0;
    }

    // ── Existing Methods ──────────────────────────────────────────

    public Transform GetFreeEndHook()
    {
        return freeEndHook;
    }

    public void SetFreeEndHook(Transform hook)
    {
        freeEndHook = hook;
        if (hook != null)
            forceY = hook.position.y;
        Debug.Log("Free end hook set to: " + hook?.name);

        UpdateDeadEndHookVisibility();
    }

    public void SetFixedPulleyRef(Transform fixedSlot)
    {
        fixedPulleySlotRef = fixedSlot;
    }

    /// <summary>
    /// Set the rope length from where the movable pulley has just been hung.
    /// Placing the pulley is what decides the length: hang it lower and the rope
    /// in that configuration is longer. The free segment equals one pulley
    /// segment, so an unloaded system hangs level.
    /// </summary>
    public void MeasureRopeLengthMA2(string reason = "unspecified")
    {
        if (fixedPulleySlotRef == null || movablePulleyBottom == null || freeEndHook == null)
            return;

        Transform movablePulley = movablePulleyBottom.parent;
        float pulleyY = movablePulley != null ? movablePulley.position.y : movablePulleyBottom.position.y;

        float d1 = Mathf.Abs(fixedPulleySlotRef.position.y - pulleyY);

        // Cap the total. As the pulley rises the free segment takes over almost
        // the whole rope, so an over-long rope lets the free end run below the
        // table however the pulley itself is placed. The pulley is moved up to
        // match, or the geometry would no longer agree with the length.
        float maxD1 = maxRopeLengthMA2 / 3f;
        if (d1 > maxD1)
        {
            d1 = maxD1;
            pulleyY = fixedPulleySlotRef.position.y - d1;
            if (movablePulley != null)
                movablePulley.position = new Vector3(
                    movablePulley.position.x, pulleyY, movablePulley.position.z);
        }

        float d2 = d1;
        totalRopeLengthMA2 = d1 * 2f + d2;
        loadY = pulleyY;
        forceY = fixedPulleySlotRef.position.y - d2;
        velocity = 0f;

        Transform freeSlot = (freeEndHook == hookLeft) ? slotLeft : slotRight;
        if (freeSlot != null)
            freeEndHook.position = new Vector3(freeSlot.position.x, forceY, freeSlot.position.z);
        else
            freeEndHook.position = new Vector3(
                freeEndHook.position.x, forceY, freeEndHook.position.z);

        Debug.Log("Rope length MA2 measured (" + reason + "): " + totalRopeLengthMA2
                  + " | d1=" + d1 + " (max " + maxD1 + ")");
    }


    // Resets only the rope's vertical geometry (movable pulley + free end Y
    // position) back to a neutral state — used between demo sequence steps so
    // weights can be re-delivered from a clean rope position, WITHOUT touching
    // the pulley's X/Z placement or lock state.
    public void ResetRopeGeometry(float neutralLoadY, float neutralForceY)
    {
        loadY = neutralLoadY;
        forceY = neutralForceY;
        velocity = 0f;

        Transform movablePulleyTransform = movablePulleyBottom != null ? movablePulleyBottom.parent : null;
        if (movablePulleyTransform != null)
            movablePulleyTransform.position = new Vector3(
                movablePulleyTransform.position.x, neutralLoadY, movablePulleyTransform.position.z);

        if (freeEndHook != null)
            freeEndHook.position = new Vector3(
                freeEndHook.position.x, neutralForceY, freeEndHook.position.z);
    }

    // ── Load-Side Trigger Drag (drives the movable pulley when it's grabbed by trigger) ──

    public void SetLoadGrabbed(bool grabbed)
    {
        loadGrabbed = grabbed;
        if (!grabbed) velocity = 0f;
    }

    public bool IsLoadGrabbed() => loadGrabbed;

    public Vector3 GetMovablePulleyPosition()
    {
        Transform movablePulley = movablePulleyBottom?.parent;
        if (movablePulley != null) return movablePulley.position;
        return movablePulleyBottom != null ? movablePulleyBottom.position : Vector3.zero;
    }

    public Transform GetFixedPulleySlotRef() 
    {
        return fixedPulleySlotRef; 
    }

    // Called every frame while the player drags the movable pulley with trigger.
    // Mirrors the freeEndGrabbed branch, but drives from the load (movable pulley) side.
    public void DragLoadTo(Vector3 desiredPosition)
    {
        if (fixedPulleySlotRef == null) return;

        float fixedY = fixedPulleySlotRef.position.y;

        float d1 = Mathf.Abs(fixedY - desiredPosition.y);
        float maxD1 = Mathf.Max(minD1, (totalRopeLengthMA2 - 0.01f) / 2f);
        d1 = Mathf.Clamp(d1, minD1, maxD1);

        loadY = fixedY - d1;

        float d2 = totalRopeLengthMA2 - d1 * 2f;
        d2 = Mathf.Max(d2, 0.01f);
        forceY = fixedY - d2;

        velocity = 0f;

        Transform movablePulley = movablePulleyBottom?.parent;
        if (movablePulley != null)
            movablePulley.position = new Vector3(desiredPosition.x, loadY, desiredPosition.z);
    }

    // ── Dead-End Hook Model Visibility ─────────────────────────────

    private void UpdateDeadEndHookVisibility()
    {
        bool isMA2 = IsMovablePulleyConfig();

        if (!isMA2)
        {
            SetHookShapeVisible(hookLeft, true);
            SetHookShapeVisible(hookRight, true);
            return;
        }

        if (freeEndHook == null) return;

        Transform deadEnd = (freeEndHook == hookLeft) ? hookRight : hookLeft;
        Transform liveEnd = (freeEndHook == hookLeft) ? hookLeft : hookRight;

        SetHookShapeVisible(liveEnd, true);
        SetHookShapeVisible(deadEnd, false);
    }

    private void SetHookShapeVisible(Transform hook, bool visible)
    {
        if (hook == null) return;
        foreach (Transform child in hook)
        {
            if (child.name.Contains("HookShape"))
                child.gameObject.SetActive(visible);
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