using UnityEngine;

/// <summary>
/// Enemy가 공격·추격할 현재 Target과 피격 경보를 관리한다.
/// 경보를 받은 Enemy는 다시 전파하지 않아 맵 전체 연쇄 감지를 막는다.
/// EnemyTurnActor는 이 컴포넌트의 Target을 읽고, BattleDamageService는 Enemy가 피해를 받았을 때 공격자를 Target으로 등록한다.
/// </summary>
public class EnemyAwareness : MonoBehaviour
{
    [Header("적 인식 설정")]
    [InspectorName("주변 경보 거리")]
    [SerializeField, Min(0f)] private float alertRange = 6f;

    public Transform Target { get; private set; }
    public bool HasTarget => Target != null;

    /// <summary>
    /// 직접 피격됐거나 주변 Enemy의 경보를 받았을 때 공격·추격 대상을 기억한다.
    /// 이 함수는 주변 경보를 다시 전파하지 않으므로 연쇄 경보가 발생하지 않는다.
    /// </summary>
    public void SetTarget(Transform target)
    {
        Target = target;
    }

    /// <summary>
    /// 기억 중인 공격·추격 대상을 제거한다.
    /// 전투 이탈이나 어그로 초기화 규칙에서 호출해야 하며, 턴이 지났다고 자동 호출되지는 않는다.
    /// </summary>
    public void ClearTarget()
    {
        Target = null;
    }

    /// <summary>DB에 정의된 주변 경보 거리를 적용한다.</summary>
    public void ConfigureAlertRange(float value)
    {
        alertRange = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 이 Enemy에게 피해를 준 공격자를 새 Target으로 등록하고 alertRange 안의 다른 Enemy에게 한 번 공유한다.
    /// BattleDamageService가 최종 피해 적용 후 호출하며, attacker가 없으면 현재 Target을 변경하지 않는다.
    /// </summary>
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
        // 피격 순간에만 실행되지만 Scene 전체 검색이다. UnitRegistry 전환 후 등록된 Enemy 목록 순회로 교체한다.
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
