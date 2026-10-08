using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine;

public class CognitiveLoadInference : MonoBehaviour
{
    public Unity.InferenceEngine.ModelAsset modelAsset;

    private Unity.InferenceEngine.Worker worker;

    private Unity.InferenceEngine.Tensor<float> inputTensor;

    private Queue<float> predictions =
        new Queue<float>();


    public int WindowSize { get; private set; } = 400;
    public int FeatureCount { get; private set; } = 7;

    public int smoothingWindow = 10;
    [Header("Smoothing window")]
    [Tooltip("Number of predictions to use in the smoothed average.")]

    public float Logit { get; private set; }
    public float Probability { get; private set; }
    public float Smooth_probability { get; private set; }



    void Start()
    {
        Unity.InferenceEngine.Model model = Unity.InferenceEngine.ModelLoader.Load(modelAsset);

        worker = new Unity.InferenceEngine.Worker(
            model,
            Unity.InferenceEngine.BackendType.GPUCompute
        );

        // inputTensor = new Unity.InferenceEngine.Tensor<float>(
        //     new Unity.InferenceEngine.TensorShape(
        //         1,
        //         windowSize,
        //         featureCount
        //     )
        // );
    }


    public void Predict(
        float[,] window, 
        out float Logit,
        out float Probability,
        out float Smooth_probability)
    {
        // Create CPU tensor with shape [1, 400, 7]
        Tensor<float> input = new Tensor<float>(
            new TensorShape(1, WindowSize, FeatureCount)
        );

        // Fill tensor
        int index = 0;

        for(int t = 0; t < WindowSize; t++)
        {
            for(int f = 0; f < FeatureCount; f++)
            {
                input[index] = window[t,f];

                index++;
            }
        }


        worker.Schedule(input);


        // Unity.InferenceEngine.Tensor<float> output =
        //     worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
        
        // float logit =
        //     output[0];

        Tensor<float> output =
            worker.PeekOutput() as Tensor<float>;

        Tensor<float> readableOutput =
            output.ReadbackAndClone();

        Logit = readableOutput[0];

        readableOutput.Dispose();
        input.Dispose();


        // sigmoid
        Probability =
            1.0f /
            (1.0f + Mathf.Exp(-Logit));

        Smooth_probability = SmoothPrediction(Probability);

        return;
    }


    float SmoothPrediction(float prediction)
    {

        predictions.Enqueue(prediction);


        if(predictions.Count >
           smoothingWindow)
        {
            predictions.Dequeue();
        }


        float sum = 0;

        foreach(float p in predictions)
        {
            sum += p;
        }


        return sum / predictions.Count;
    }


    void OnDestroy()
    {
        worker?.Dispose();

        inputTensor?.Dispose();
    }
}