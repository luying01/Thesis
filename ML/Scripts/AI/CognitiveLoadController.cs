using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using System.Collections;

public class CognitiveLoadController : MonoBehaviour
{
    public EyeBuffer buffer;
    public CognitiveLoadInference model;
    public PupilBaselineCalibrator calibrator;


    void Start()
    {
        StartCoroutine(RunCalibration());
    }

    IEnumerator RunCalibration()
    {
        yield return StartCoroutine(calibrator.Calibrate());

        Debug.Log("Left: " + buffer.LeftBaseline);
        Debug.Log("Right: " + buffer.RightBaseline);
    }

    void Update()
    {
        buffer.AddSample();

        if(calibrator.Finished && buffer.IsFull())
        {

            float[,] window =
                buffer.GetWindow();


            float load =
                model.Predict(window);


            Debug.Log(
                "Cognitive load probability: "
                + load
            );
        }
    }
}