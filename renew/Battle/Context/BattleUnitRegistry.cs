using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>현재 전투에 참여하는 Player와 Enemy 참조를 등록하고 조회한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleUnitRegistry : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private List<GameObject> enemies = new List<GameObject>();

    public GameObject Player => player;
    public IReadOnlyList<GameObject> Enemies => enemies;

    public event Action<GameObject> PlayerChanged;
    public event Action<GameObject> EnemyRegistered;
    public event Action<GameObject> EnemyUnregistered;

    /// <summary>현재 전투에서 사용하는 Player Body를 등록하고 이후 조회 기준으로 사용한다.</summary>
    public void RegisterPlayer(GameObject target)
    {
        if (player == target)
        {
            return;
        }

        player = target;
        PlayerChanged?.Invoke(player);
    }

    /// <summary>Enemy를 중복 없이 등록한다. 실제로 목록이 변경된 경우에만 true를 반환한다.</summary>
    public bool RegisterEnemy(GameObject enemy)
    {
        RemoveMissingEnemies();
        if (enemy == null || enemies.Contains(enemy))
        {
            return false;
        }

        enemies.Add(enemy);
        EnemyRegistered?.Invoke(enemy);
        return true;
    }

    /// <summary>사망·제거된 Enemy를 등록 목록에서 해제한다. 제거된 항목이 있으면 true를 반환한다.</summary>
    public bool UnregisterEnemy(GameObject enemy)
    {
        if (enemy == null || !enemies.Remove(enemy))
        {
            return false;
        }

        EnemyUnregistered?.Invoke(enemy);
        return true;
    }

    /// <summary>Unity에서 파괴되어 null로 남은 Enemy 참조를 목록에서 정리한다.</summary>
    public void RemoveMissingEnemies()
    {
        enemies.RemoveAll(enemy => enemy == null);
    }

    /// <summary>Scene 종료 또는 전투 재초기화 시 Player와 Enemy 등록 정보를 모두 비운다.</summary>
    public void Clear()
    {
        player = null;
        enemies.Clear();
    }
}
