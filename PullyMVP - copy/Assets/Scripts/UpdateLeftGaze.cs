using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class NewBehaviourScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] out_gazes);
        XrSingleEyeGazeDataHTC leftGaze = out_gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
        if (leftGaze.isValid)
        {
            // Eye position in world space
            Vector3 gazePosition = leftGaze.gazePose.position.ToUnityVector() + new Vector3(-0.75f, 1.2f, 0.75f);

            // Gaze orientation in world space
            Quaternion gazeOrientation = Quaternion.Euler(0, 135f, 0) * leftGaze.gazePose.orientation.ToUnityQuaternion();

            // Place object 1 meter along gaze direction
            transform.position = gazePosition + gazeOrientation * Vector3.forward * 1f;
            transform.rotation = gazeOrientation;
        }
    }
}
