using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class UpdateRightPupil : MonoBehaviour
{
    float rightPupilDiameter;
    XrVector2f rightPupilPosition;

    // Start is called before the first frame update
    void Start()
    {
     
    }

    // Update is called once per frame
    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] out_pupils);
        XrSingleEyePupilDataHTC rightPupil = out_pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];
        if (rightPupil.isDiameterValid)
            rightPupilDiameter = rightPupil.pupilDiameter;
            Debug.Log("Right pupil diameter: " + rightPupilDiameter);
        //Do something
        if (rightPupil.isPositionValid)
            rightPupilPosition = rightPupil.pupilPosition;
        //Do something 
            Debug.Log("Right pupil position: " + rightPupilPosition.x + ", " + rightPupilPosition.y);
    }
}
