using UnityEngine;


public class EyeBuffer : MonoBehaviour
{
    public HTCHeadsetReader reader;
    public PupilBaselineCalibrator calibrator;

    public float LeftBaseline { get; set; }
    public float RightBaseline { get; set; }

    public int windowSize = 400;
    public int features = 7;

    private float[,] buffer;

    private int index = 0;

    void Awake()
    {
        buffer =
            new float[
                windowSize,
                features
            ];
    }



    public void AddSample()
    {
        buffer[index, 0] =
            reader.LeftPupilDiameter / LeftBaseline;

        buffer[index, 1] =
            reader.RightPupilDiameter / RightBaseline;

        buffer[index, 2] = reader.GazeX;
        buffer[index, 3] = reader.GazeY;
        buffer[index, 4] = reader.GazeZ;

        buffer[index, 5] = reader.LeftEyeOpenness;
        buffer[index, 6] = reader.RightEyeOpenness;

        // for(int f=0;f<features;f++)
        // {
        //     buffer[index,f] =
        //         sample[f];
        // }


        index++;

        if(index >= windowSize)
            index = 0;
    }



    public bool IsFull() // This may need to be reworked
    {
        return index == 0;
    }



    public float[,] GetWindow()
    {

        float[,] output =
            new float[
                windowSize,
                features
            ];


        // reorder circular buffer
        for(int i=0;i<windowSize;i++)
        {
            int source =
                (index+i)
                % windowSize;


            for(int f=0;f<features;f++)
            {
                output[i,f] =
                    buffer[source,f];
            }
        }


        return output;
    }
}