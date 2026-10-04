using UnityEngine;

/// <summary>
/// CL thresholds for the three-level fidelity ladder.
///
///   Level 2 = high interaction fidelity, conceptual aid OFF
///   Level 1 = high interaction fidelity, conceptual aid ON
///   Level 0 = low interaction fidelity (system places equipment), aid ON
///
/// The 0-100 CL range is split into three equal bands. Each boundary gets a
/// symmetric buffer (dead band) so a score hovering near a boundary does not
/// make the level flicker:
///
///   switch DOWN a level (CL rising)  when CL >= boundary + buffer
///   switch UP   a level (CL falling) when CL <  boundary - buffer
///
/// With the defaults: 2->1 at 38.3, 1->2 below 28.3,
///                    1->0 at 71.7, 0->1 below 61.7.
/// </summary>
[CreateAssetMenu(fileName = "FidelityConfig", menuName = "AdaptiveSystem/FidelityConfig")]
public class FidelityConfig : ScriptableObject
{
    [Header("Band Boundaries (0-100, three equal bands)")]
    [Tooltip("Boundary between Level 2 (no aid) and Level 1 (aid).")]
    public float boundary2to1 = 33.33f;
    [Tooltip("Boundary between Level 1 (aid) and Level 0 (low fidelity + aid).")]
    public float boundary1to0 = 66.67f;

    [Header("Buffer (dead band, applied on each side of a boundary)")]
    [Tooltip("Half-width of the dead band. 5 means a 10-point band in total.")]
    public float buffer = 5f;
}