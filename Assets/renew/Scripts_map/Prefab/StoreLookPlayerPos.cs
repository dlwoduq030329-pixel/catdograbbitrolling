using UnityEngine;

public class StoreLookPlayerPos : MonoBehaviour
{
    private void Start()
    {
        // Player의 실제 위치 주체가 시각 Prefab에서 Player Body로 변경되었다.
        // 전투 등록이 먼저 끝난 경우에는 Registry의 Body를 사용하고,
        // 상점이 더 먼저 초기화된 경우에는 Scene의 Player 태그를 가진 Body를 사용한다.
        GameObject playerBody = BattleGameManager.Instance != null
            ? BattleGameManager.Instance.CurrentPlayer
            : null;

        if (playerBody == null)
        {
            playerBody = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerBody == null)
        {
            Debug.LogWarning("상점이 바라볼 Player Body를 찾지 못했습니다.", this);
            return;
        }

        Vector3 targetPosition = playerBody.transform.position;
        targetPosition.y = transform.position.y;
        transform.LookAt(targetPosition);
    }
}
