using Unity;
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
    public FidelityManager fidelityManager;
    private int currentWindowId = 0;

    private float logit;
    private float probability;
    private float smooth_probability;
    public float Prediction { get; private set; }
    [Header("Begin measuring and logging samples")]
    [Tooltip("Should be on by default. Used to begin and log measurements. Only disable if not wanting to collect measurements.")]
    public bool BeginMeasuring = false;
    


    void Start()
    {
        logger.StartLogging();
        StartCoroutine(RunCalibration());
    }

    IEnumerator RunCalibration()
    {
        yield return calibrator.Calibrate();

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
        if (calibrator.Finished && BeginMeasuring)
        {
            buffer.AddSample(reader.Timestamp);

            logger.LogMeasurement(
                reader, 
                reader.LeftPupilDiameter / buffer.LeftBaseline, 
                reader.RightPupilDiameter / buffer.RightBaseline);

            if(buffer.IsFull())
            {
                EyeWindow window = buffer.GetWindow();

                model.Predict(window.Data, out logit, out probability, out smooth_probability);

                Prediction = smooth_probability;
                if (currentWindowId > 5) // ignore the first couple of measurements to get stable predictions before setting the fidelity manager CL score 
                {
                    fidelityManager.SetCLScore(Prediction * 100);
                }

                logger.LogPrediction(
                    currentWindowId,
                    window.StartTimestamp,
                    window.EndTimestamp,
                    logit,
                    probability,
                    smooth_probability);


                Debug.Log(
                    "Cognitive load probability: "
                    + probability
                );

                currentWindowId++;

                // Debug.Log("Logging at: " + Application.persistentDataPath);
            }
        }   
    }
}