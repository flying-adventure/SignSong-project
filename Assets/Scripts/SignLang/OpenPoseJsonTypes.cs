using System;
using UnityEngine;

// ---------- 카메라 파라미터 ----------

[Serializable]
public class OpenPoseCamParamIntrinsics
{
    public string data;
}

[Serializable]
public class OpenPoseCamParamCameraMatrix
{
    public string data;
}

[Serializable]
public class OpenPoseCamParamDistortion
{
    public string rows;
    public string data;
}

[Serializable]
public class OpenPoseCamParam
{
    public OpenPoseCamParamIntrinsics Intrinsics;
    public OpenPoseCamParamCameraMatrix CameraMatrix;
    public OpenPoseCamParamDistortion Distortion;
}

// ---------- 사람 1명에 대한 키포인트 ----------

[Serializable]
public class OpenPosePerson
{
    public int person_id;

    public float[] face_keypoints_2d;
    public float[] pose_keypoints_2d;
    public float[] hand_left_keypoints_2d;
    public float[] hand_right_keypoints_2d;

    public float[] face_keypoints_3d;
    public float[] pose_keypoints_3d;
    public float[] hand_left_keypoints_3d;
    public float[] hand_right_keypoints_3d;
}

// ---------- 루트 오브젝트 ----------

[Serializable]
public class OpenPoseRoot
{
    public float version;
    public OpenPosePerson people;   // NIA JSON: "people": { ... }
    public OpenPoseCamParam camparam;
}