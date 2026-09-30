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


    public int windowSize = 400;
    public int featureCount = 7;

    public int smoothingWindow = 10;

    public float logit;
    public float probability;
    public float smooth_probability;



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
        out float logit,
        out float probability,
        out float smooth_probability)
    {
        // Create CPU tensor with shape [1, 400, 7]
        Tensor<float> input = new Tensor<float>(
            new TensorShape(1, windowSize, featureCount)
        );

        // Fill tensor
        int index = 0;

        for(int t = 0; t < windowSize; t++)
        {
            for(int f = 0; f < featureCount; f++)
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

        logit = readableOutput[0];

        readableOutput.Dispose();
        input.Dispose();


        // sigmoid
        probability =
            1.0f /
            (1.0f + Mathf.Exp(-logit));

        smooth_probability = SmoothPrediction(probability);

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