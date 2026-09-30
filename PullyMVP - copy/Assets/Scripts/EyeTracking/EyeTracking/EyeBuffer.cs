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
    private long[] timestamps;

    void Awake()
    {
        buffer =
            new float[
                windowSize,
                features
            ];

        timestamps = new long[windowSize];
    }



    public void AddSample(long timestamp)
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

        timestamps[index] = timestamp;

        index = (index + 1) % windowSize;

        // index++;

        // if(index >= windowSize)
        //     index = 0;
    }



    public bool IsFull()
    {
        return index == 0;
    }



//     public float[,] GetWindow(
//         out long startTimestamp,
//         out long endTimestamp
//     )
//     {

//         float[,] output =
//             new float[
//                 windowSize,
//                 features
//             ];


//         // reorder circular buffer
//         for(int i=0;i<windowSize;i++)
//         {
//             int source =
//                 (index+i)
//                 % windowSize;


//             for(int f=0;f<features;f++)
//             {
//                 output[i,f] =
//                     buffer[source,f];
//             }
//         }


//         return output;
//     }
// }

    public EyeWindow GetWindow()
    {
        float[,] output =
            new float[
                windowSize,
                features
            ];

        long startTimestamp = 0;
        long endTimestamp = 0;

        // Reorder circular buffer
        for (int i = 0; i < windowSize; i++)
        {
            int source =
                (index + i) % windowSize;

            for (int f = 0; f < features; f++)
            {
                output[i, f] =
                    buffer[source, f];
            }

            if (i == 0)
                startTimestamp = timestamps[source];

            if (i == windowSize - 1)
                endTimestamp = timestamps[source];
        }

        return new EyeWindow
        {
            Data = output,
            StartTimestamp = startTimestamp,
            EndTimestamp = endTimestamp
        };
    }
}