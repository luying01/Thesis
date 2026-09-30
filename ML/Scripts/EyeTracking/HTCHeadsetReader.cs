using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;


public class HTCHeadsetReader : MonoBehaviour
{
    public EyeBuffer buffer;

    public float GazeX { get; private set; }
    public float GazeY { get; private set; }
    public float GazeZ { get; private set; }
    public float LeftPupilDiameter { get; private set; }
    public float RightPupilDiameter { get; private set; }
    public float LeftEyeOpenness { get; private set; }
    public float RightEyeOpenness { get; private set; }

    public bool HasValidPupils { get; private set; }

    public long timestamp;

    // public float[] CurrentFeatureVector();


    void Update() 
    {
        // Gaze
        //
        XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] out_gazes);


        XrSingleEyeGazeDataHTC leftGaze =
            out_gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];


        if(leftGaze.isValid) // We are currently only using the left eye for gaze direction
        {
            Quaternion direction =
                Quaternion.Euler(0, 135f, 0) * leftGaze.gazePose.orientation.ToUnityQuaternion(); // may need to update the values


            GazeX = direction.x;
            GazeY = direction.y;
            GazeZ = direction.z;
        }

        
        // Pupils
        //
        XR_HTC_eye_tracker.Interop.GetEyePupilData(
            out XrSingleEyePupilDataHTC[] pupils
        );


        XrSingleEyePupilDataHTC leftPupilData =
            pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];


        XrSingleEyePupilDataHTC rightPupilData =
            pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];

        if((!leftPupilData.isDiameterValid) || (!rightPupilData.isDiameterValid))
        {
            HasValidPupils = false;
        }
        else
        {
            HasValidPupils = true;
        }


        if(leftPupilData.isDiameterValid)
        {
            LeftPupilDiameter =
                leftPupilData.pupilDiameter;
        }


        if(rightPupilData.isDiameterValid)
        {
            RightPupilDiameter =
                rightPupilData.pupilDiameter;
        }


        // Eye openness
        //
        XR_HTC_eye_tracker.Interop.GetEyeGeometricData(
            out XrSingleEyeGeometricDataHTC[] geometrics
        );

        XrSingleEyeGeometricDataHTC leftEye =
            geometrics[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];

        XrSingleEyeGeometricDataHTC rightEye =
            geometrics[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];

        if(leftEye.isValid)
        {
            LeftEyeOpenness = leftEye.eyeOpenness;
        }

        if(rightEye.isValid)
        {
            RightEyeOpenness = rightEye.eyeOpenness;
        }

        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}