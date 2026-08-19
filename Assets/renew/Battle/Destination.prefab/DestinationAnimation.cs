using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택한 이동 목적지 화살표를 위아래로 반복 이동시키는 표시 전용 애니메이션이다.
/// </summary>

public class DestinationAnimation : MonoBehaviour
{
    [InspectorName("상하 이동 높이")]
    [SerializeField]
    float offSet = 0.7f;
    [InspectorName("상하 이동 속도")]
    [SerializeField]
    float speed = 1f;

    bool movingUp = true;

    /// <summary>활성화될 때 반복 이동 Coroutine을 시작한다.</summary>
    void OnEnable()
    {
        StartCoroutine(MoveUpDown());
    }

    /// <summary>초기 높이와 Offset 높이 사이를 계속 왕복한다.</summary>
    IEnumerator MoveUpDown()
    {
        float y = transform.position.y;
        while (true)
        {
            // 현재 이동 방향에 따라 위쪽 또는 원래 높이를 목표로 선택한다.
            float targetY = movingUp ? y + offSet : y;

            // Frame마다 목표 높이로 일정 속도로 이동한다.
            while (Mathf.Abs(transform.position.y - targetY) > 0.1f)
            {
                float newY = Mathf.MoveTowards(transform.position.y, targetY, speed * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
                yield return null;  // 다음 Frame까지 대기한다.
            }

            // 목표에 도달하면 이동 방향을 반대로 전환한다.
            movingUp = !movingUp;

            // 방향 전환이 보이도록 잠깐 대기한다.
            yield return new WaitForSeconds(0.1f);
        }
    }
}
