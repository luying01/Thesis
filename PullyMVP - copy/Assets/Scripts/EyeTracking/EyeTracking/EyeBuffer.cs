using UnityEngine;


public class EyeBuffer : MonoBehaviour
{
    public HTCHeadsetReader reader;
    public PupilBaselineCalibrator calibrator;

    public float LeftBaseline { get; set; }
    public float RightBaseline { get; set; }

    public int WindowSize { get; private set; } = 400;
    public int Features { get; private set;} = 7;

    private float[,] buffer;

    private int index = 0;
    private long[] timestamps;

    void Awake()
    {
        buffer =
            new float[
                WindowSize,
                Features
            ];

        timestamps = new long[WindowSize];
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

        index = (index + 1) % WindowSize;

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
                WindowSize,
                Features
            ];

        long startTimestamp = 0;
        long endTimestamp = 0;

        // Reorder circular buffer
        for (int i = 0; i < WindowSize; i++)
        {
            int source =
                (index + i) % WindowSize;

            for (int f = 0; f < Features; f++)
            {
                output[i, f] =
                    buffer[source, f];
            }

            if (i == 0)
                startTimestamp = timestamps[source];

            if (i == WindowSize - 1)
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