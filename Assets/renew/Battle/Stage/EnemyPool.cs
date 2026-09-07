using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>종류별 고정 풀. 대기분을 포함한 상한이며 실행 중 자동 확장하지 않는다.</summary>
public sealed class EnemyPool : MonoBehaviour
{
    [SerializeField, Min(0), Tooltip("일반 적 한 종류마다 준비할 수량. Play 시작 전에 변경합니다.")]
    private int normalPerType = 5;
    [SerializeField, Min(0)] private int elitePerType = 2;
    [SerializeField, Min(0)] private int bossPerType = 1;
    private readonly Dictionary<BattleEnemyData, List<GameObject>> pools = new Dictionary<BattleEnemyData, List<GameObject>>();

    public void Prepare(BattleEnemyDatabase database, Func<BattleEnemyData, GameObject> create)
    {
        if (database == null || create == null) return;
        for (int i = 0; i < database.Count; i++)
        {
            BattleEnemyData data = database.GetAt(i);
            if (data == null || data.prefab == null || data.spawnRole != SpawnRole.Enemy || pools.ContainsKey(data)) continue;
            int capacity = data.rank == BattleEnemyRank.Boss ? bossPerType :
                data.rank == BattleEnemyRank.Elite ? elitePerType : normalPerType;
            var entries = new List<GameObject>();
            pools.Add(data, entries);
            for (int j = 0; j < capacity; j++)
            {
                GameObject enemy = create(data);
                if (enemy == null) break;
                enemy.SetActive(false);
                entries.Add(enemy);
            }
        }
    }

    public GameObject Take(BattleEnemyData data)
    {
        if (data == null || !pools.TryGetValue(data, out List<GameObject> entries)) return null;
        foreach (GameObject enemy in entries)
            if (enemy != null && !enemy.activeSelf) return enemy;
        return null;
    }

    public int Available(BattleEnemyData data)
    {
        if (data == null || !pools.TryGetValue(data, out List<GameObject> entries)) return 0;
        int count = 0;
        foreach (GameObject enemy in entries)
            if (enemy != null && !enemy.activeSelf) count++;
        return count;
    }
}
