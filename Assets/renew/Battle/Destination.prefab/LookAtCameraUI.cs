using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 목적지 표시의 회전 방식을 제어한다. 화살표 사용 시에는 고정 회전을 유지한다.
/// </summary>

public class LookAtCameraUI : MonoBehaviour
{
    [InspectorName("카메라 회전 따라가기")]
    [SerializeField] private bool followCameraRotation = false;
    [InspectorName("고정 회전값")]
    [SerializeField] private Vector3 fixedRotation = Vector3.zero;

    void Update()
    {
        if (followCameraRotation && Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
            return;
        }

        transform.rotation = Quaternion.Euler(fixedRotation);
    }
}
