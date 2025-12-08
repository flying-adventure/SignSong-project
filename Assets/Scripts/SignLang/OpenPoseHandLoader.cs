using System;
using UnityEngine;

public class OpenPoseHandLoader : MonoBehaviour
{
    [Header("Debug용 초기 JSON (필수 아님)")]
    public TextAsset initialJson;

    [Header("3D Keypoints (OpenPose 3D 원본 값)")]
    public Vector3[] body3D;
    public Vector3[] leftHand3D;
    public Vector3[] rightHand3D;

    private const int BodyJointCount = 25;   // OpenPose BODY_25
    private const int HandJointCount = 21;   // OpenPose hand

    private void Start()
    {
        if (initialJson != null)
        {
            Debug.Log("[Loader] Start()에서 initialJson 로드");
            LoadFromJsonText(initialJson.text);
        }
    }

    /// <summary>
    /// 한 프레임의 OpenPose JSON 텍스트를 받아서 3D 배열로 파싱
    /// </summary>
    public void LoadFromJsonText(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[Loader] 빈 JSON 입니다.");
            return;
        }

        try
        {
            var root = JsonUtility.FromJson<OpenPoseRoot>(json);
            if (root == null)
            {
                Debug.LogWarning("[Loader] 루트 파싱 실패 (null)");
                return;
            }

            if (root.people == null)
            {
                Debug.LogWarning("[Loader] root.people 가 null 입니다.");
                return;
            }

            var person = root.people;

            // 3D 기반으로 사용
            PoseKeypointsTo3D(person.pose_keypoints_3d, ref body3D, BodyJointCount);
            PoseKeypointsTo3D(person.hand_left_keypoints_3d, ref leftHand3D, HandJointCount);
            PoseKeypointsTo3D(person.hand_right_keypoints_3d, ref rightHand3D, HandJointCount);

            int bodyLen = body3D != null ? body3D.Length : 0;
            int leftLen = leftHand3D != null ? leftHand3D.Length : 0;
            int rightLen = rightHand3D != null ? rightHand3D.Length : 0;

            Debug.Log($"[Loader] 프레임 로드 완료: body={bodyLen}, leftHand={leftLen}, rightHand={rightLen}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Loader] JSON 파싱 오류\n{ex}");
        }
    }

    /// <summary>
    /// OpenPose 3D 배열 (x,y,z,c) * n → Vector3[n]
    /// </summary>
    private void PoseKeypointsTo3D(float[] src, ref Vector3[] dst, int jointCount)
    {
        if (src == null || src.Length == 0)
        {
            dst = null;
            return;
        }

        int stride = 4; // x, y, z, confidence

        if (dst == null || dst.Length != jointCount)
            dst = new Vector3[jointCount];

        for (int i = 0; i < jointCount; i++)
        {
            int idx = i * stride;
            if (idx + 2 >= src.Length)
                break;

            float x = src[idx + 0];
            float y = src[idx + 1];
            float z = src[idx + 2];

            dst[i] = new Vector3(x, y, z);
        }
    }
}