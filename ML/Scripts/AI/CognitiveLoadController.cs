using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using System.Collections;

public class CognitiveLoadController : MonoBehaviour
{
    public EyeBuffer buffer;
    public CognitiveLoadInference model;
    public PupilBaselineCalibrator calibrator;
    public HTCHeadsetReader reader;
    public ExperimentLogger logger;
    private int currentWindowId = 0;

    private float logit;
    private float probability;
    private float smooth_probability;


    void Start()
    {
        logger.StartLogging();
        StartCoroutine(RunCalibration());
    }

    IEnumerator RunCalibration()
    {
        yield return StartCoroutine(calibrator.Calibrate());

        logger.LogBaseline(
            buffer.LeftBaseline,
            buffer.RightBaseline
        );

        Debug.Log("Calibrating complete");

        Debug.Log("Left: " + buffer.LeftBaseline);
        Debug.Log("Right: " + buffer.RightBaseline);
    }

    void Update()
    {
        if (calibrator.Finished)
        {
            buffer.AddSample(reader.timestamp);

            logger.LogMeasurement(
                reader, 
                reader.LeftPupilDiameter / buffer.LeftBaseline, 
                reader.RightPupilDiameter / buffer.RightBaseline);

            if(buffer.IsFull())
            {

                EyeWindow window = buffer.GetWindow();

                // int currentWindowId = windowId++;

                model.Predict(window.Data, out logit, out probability, out smooth_probability);

                logger.LogPrediction(
                    currentWindowId,
                    window.StartTimestamp,
                    window.EndTimestamp,
                    logit,
                    probability,
                    smooth_probability);


                Debug.Log(
                    "Cognitive load probability: "
                    + model.probability
                );

                currentWindowId++;

                Debug.Log("Logging at: " + Application.persistentDataPath);
            }
        }   
    }
}