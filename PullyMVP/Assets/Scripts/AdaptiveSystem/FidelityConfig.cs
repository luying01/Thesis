using UnityEngine;

[CreateAssetMenu(fileName = "FidelityConfig", menuName = "AdaptiveSystem/FidelityConfig")]
public class FidelityConfig : ScriptableObject
{
    [Header("CL Thresholds (0-100)")]
    public float toLevel2 = 35f;    // Level 3 -> Level 2
    public float toLevel1 = 58f;    // Level 2 -> Level 1
    public float toLevel0 = 78f;    // Level 1 -> Level 0

    [Header("Hysteresis")]  //(·À¶¶Çø¼ä)
    public float hysteresis = 10f;
}