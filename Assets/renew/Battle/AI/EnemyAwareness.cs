using UnityEngine;

/// <summary>
/// Enemy가 기억하는 현재 Target과 피격 경보를 관리한다.
/// 경보를 받은 Enemy는 다시 전파하지 않아 맵 전체 연쇄 감지를 막는다.
/// </summary>
public class EnemyAwareness : MonoBehaviour
{
    [Header("적 인식 설정")]
    [InspectorName("주변 경보 거리")]
    [SerializeField, Min(0f)] private float alertRange = 6f;

    public Transform Target { get; private set; }
    public bool HasTarget => Target != null;

    /// <summary>직접 감지하거나 경보로 전달받은 Target을 기억한다.</summary>
    public void SetTarget(Transform target)
    {
        Target = target;
    }

    /// <summary>기억 중인 플레이어 대상을 제거해 적을 비인식 상태로 되돌린다.</summary>
    public void ClearTarget()
    {
        Target = null;
    }

    /// <summary>DB에 정의된 주변 경보 거리를 적용한다.</summary>
    public void ConfigureAlertRange(float value)
    {
        alertRange = Mathf.Max(0f, value);
    }

    /// <summary>공격자를 Target으로 등록하고 가까운 Enemy에게 한 번만 공유한다.</summary>
    public void NotifyDamaged(Transform attacker)
    {
        if (attacker == null)
        {
            return;
        }

        SetTarget(attacker);
        AlertNearbyEnemies(attacker);
    }

    /// <summary>피격 경보 발생 시 설정된 거리 안의 다른 적에게 플레이어 대상을 한 번 공유한다.</summary>
    private void AlertNearbyEnemies(Transform target)
    {
        EnemyAwareness[] enemies = FindObjectsByType<EnemyAwareness>(FindObjectsSortMode.None);
        foreach (EnemyAwareness enemy in enemies)
        {
            if (enemy == null || enemy == this)
            {
                continue;
            }

            Vector2 difference = new Vector2(
                enemy.transform.position.x - transform.position.x,
                enemy.transform.position.z - transform.position.z);

            if (difference.magnitude <= alertRange)
            {
                // 경보 수신자는 NotifyDamaged를 호출하지 않으므로 경보가 연쇄 전파되지 않는다.
                enemy.SetTarget(target);
            }
        }
    }
}
