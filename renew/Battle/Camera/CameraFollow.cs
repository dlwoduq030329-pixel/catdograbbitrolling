using UnityEngine;

/// <summary>대상 위치에 보정값을 더한 지점을 보간 이동으로 추적한다.</summary>
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상 참조")]
    [InspectorName("추적 대상")]
    public Transform target;

    [Header("추적 설정")]
    [InspectorName("카메라 위치 보정값")]
    public Vector3 offset = new Vector3(0f, 12f, -10f);
    [InspectorName("추적 보간 속도")]
    public float followSpeed = 8f;
    [InspectorName("대상 바라보기")]
    public bool lookAtTarget = false;

    /// <summary>플레이어 이동이 반영된 뒤 대상 위치와 수동 보정값을 사용해 카메라를 따라간다.</summary>
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        if (lookAtTarget)
        {
            transform.LookAt(target);
        }
    }
}
