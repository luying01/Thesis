using System;
using System.IO;
using UnityEngine;

public class ExperimentLogger : MonoBehaviour
{
    [Header("Participant")]
    public int participantId = 1;

    public string Folder { get; private set; }
    public string DataFolder { get; private set; }
    private string calibrationPath;
    private string measurementPath;
    private string predictionPath;

    private StreamWriter calibrationWriter;
    private StreamWriter measurementWriter;
    private StreamWriter predictionWriter;

    public void StartLogging()
    {
        Folder = Path.Combine(
            Application.persistentDataPath, 
            $"Participant_{participantId}"
        );
        
        DataFolder = Path.Combine(
            Folder,
            "CognitiveLoadData"
        );

        Directory.CreateDirectory(DataFolder);

        calibrationPath = Path.Combine(
            DataFolder,
            $"participant_{participantId}_calibration.csv"
        );

        measurementPath = Path.Combine(
            DataFolder,
            $"participant_{participantId}_measurements.csv"
        );

        predictionPath = Path.Combine(
            DataFolder,
            $"participant_{participantId}_predictions.csv"
        );
        
        calibrationWriter = new StreamWriter(
            calibrationPath,
            false
        );

        measurementWriter = new StreamWriter(
            measurementPath,
            false
        );

        predictionWriter = new StreamWriter(
            predictionPath,
            false
        );

        calibrationWriter.WriteLine(
            "Timestamp,ParticipantId,LeftPupil,RightPupil"
        );

        measurementWriter.WriteLine(
            // "WindowID," +
            "Timestamp,ParticipantId," +
            "LeftPupil,RightPupil," +
            // "LeftPupilNorm,RightPupilNorm," +
            "GazeX,GazeY,GazeZ," +
            // "RightGazeX,RightGazeY,RightGazeZ," +
            "LeftEyeOpenness,RightEyeOpenness"
        );

        predictionWriter.WriteLine(
            "window_id," + 
            "StartTimestamp," +
            "EndTimestamp," +
            "ParticipantID," +
            "logit," +
            "probability," +
            "smooth_probability"
        );
    }

    public void LogCalibrationSample(
        float leftPupil,
        float rightPupil
    )
    {
        calibrationWriter.WriteLine(
            $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}," +
            $"{participantId}," +
            $"{leftPupil:F5}," +
            $"{rightPupil:F5}"
        );
    }

    public void LogMeasurement(
        HTCHeadsetReader reader,
        float leftPupilNorm,
        float rightPupilNorm
    )
    {
        measurementWriter.WriteLine(
            $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}," +
            $"{participantId}," +
            $"{reader.LeftPupilDiameter:F5}," +
            $"{reader.RightPupilDiameter:F5}," +
            $"{leftPupilNorm:F5}," +
            $"{rightPupilNorm:F5}," +
            $"{reader.GazeX:F5}," +
            $"{reader.GazeY:F5}," +
            $"{reader.GazeZ:F5}," +
            // $"{reader.RightGazeX:F5}," +
            // $"{reader.RightGazeY:F5}," +
            // $"{reader.RightGazeZ:F5}," +
            $"{reader.LeftEyeOpenness:F5}," +
            $"{reader.RightEyeOpenness:F5}"
        );
    }

    public void LogPrediction(
        int window_id,
        long start_timestamp,
        long end_timestamp,
        float logit,
        float prediction,
        float smooth_probability
    )
    {
        predictionWriter.WriteLine(
            $"{window_id:F5}," +
            $"{start_timestamp:F5}," +
            $"{end_timestamp:F5}," +
            $"{participantId}," +
            $"{logit:F5}," +
            $"{prediction:F5}," +
            $"{smooth_probability:F5}"
        );
    }

    public void LogBaseline(
        float leftBaseline,
        float rightBaseline
    )
    {
        calibrationWriter.WriteLine(
            $"BASELINE,{participantId}," +
            $"{leftBaseline:F5}," +
            $"{rightBaseline:F5}"
        );
    }

    private void OnDestroy()
    {
        calibrationWriter?.Flush();
        calibrationWriter?.Close();

        measurementWriter?.Flush();
        measurementWriter?.Close();

        predictionWriter?.Flush();
        predictionWriter?.Close();
    }
}