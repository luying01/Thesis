using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class PupilBaselineCalibrator : MonoBehaviour
{
    public HTCHeadsetReader reader;
    public EyeBuffer buffer;
    public ExperimentLogger logger;
    [Header("Calibration time")]
    [Tooltip("Number of seconds for the user to adapt their eyes. Only the last 20 seconds are measured for calibration.")]
    public int seconds = 300;
    [Header("Initialize calibration routine")]
    public bool BeginCalibration = false;
    [Header("Stop calibration timer early")]
    [Tooltip("Keep off by default. Enable to immediately begin the 20 second calibration timer.")]
    public bool EndCalibrationEarly = false;

    private float leftSum;
    private float rightSum;

    private int sampleCount;

    private float Left_Baseline;
    private float Right_Baseline;

    public bool Finished { get; private set; }

    public float LeftBaseline { get; private set; }
    public float RightBaseline { get; private set; }

    public IEnumerator Calibrate()
    {
        yield return new WaitUntil(() => BeginCalibration);
        
        Debug.Log("Calibrating...");
        Finished = false;

        leftSum = 0;
        rightSum = 0;
        sampleCount = 0;
        Left_Baseline = 0.0f;
        Right_Baseline = 0.0f;

        float endTime = Time.time + seconds; // number of seconds to relax

        while (Time.time < endTime)
        {
            if (EndCalibrationEarly && endTime > Time.time + 20)
            {
                endTime = Time.time + 20;
            }

            if (reader.HasValidPupils && (Time.time >= endTime - 20)) // Only use last 20 seconds to get baseline in similar lighting conditions
            {
                leftSum += reader.LeftPupilDiameter;
                rightSum += reader.RightPupilDiameter;
                sampleCount++;

                logger.LogCalibrationSample(
                    reader.LeftPupilDiameter,
                    reader.RightPupilDiameter
                );


                Debug.Log("Sample count: " + sampleCount);
            }

            yield return null;
        }

        if (sampleCount != 0)
        {
            Left_Baseline = leftSum / sampleCount;
            Right_Baseline = rightSum / sampleCount;
        }
        

        buffer.LeftBaseline = Left_Baseline;
        buffer.RightBaseline = Right_Baseline;

        Finished = true;
    }
}