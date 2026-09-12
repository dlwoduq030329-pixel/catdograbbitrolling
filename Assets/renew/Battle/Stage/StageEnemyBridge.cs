using System.Collections.Generic;
using UnityEngine;

/// <summary>Stage 설정을 검증하고 추첨한 적 목록으로 변환한다. 잘못된 후보만 제외한다.</summary>
public static class StageEnemyBridge
{
    public static bool TryBuild(StageObjData stage, List<BattleEnemyData> result, out string message,
        System.Func<BattleEnemyData, int> available = null)
    {
        result.Clear();
        if (stage == null || stage.enemyDatabase == null)
        { message = "Stage 또는 Enemy DB 참조가 없습니다."; return false; }
        var lookup = new Dictionary<string, BattleEnemyData>();
        var duplicates = new HashSet<string>();
        for (int i = 0; i < stage.enemyDatabase.Count; i++)
        {
            BattleEnemyData data = stage.enemyDatabase.GetAt(i);
            if (data == null || string.IsNullOrWhiteSpace(data.id)) continue;
            if (lookup.ContainsKey(data.id)) duplicates.Add(data.id);
            else lookup.Add(data.id, data);
        }
        foreach (string id in duplicates) lookup.Remove(id);  
        var warnings = new List<string>();
        Append(stage.normalEnemies, BattleEnemyRank.Normal, lookup, result, warnings, available);
        Append(stage.eliteEnemies, BattleEnemyRank.Elite, lookup, result, warnings, available);
        Append(stage.bossEnemies, BattleEnemyRank.Boss, lookup, result, warnings, available);
        message = warnings.Count == 0 ? null : string.Join("\n", warnings);
        return true;
    }

    private static void Append(StageEnemyGroup group, BattleEnemyRank rank,
        Dictionary<string, BattleEnemyData> lookup, List<BattleEnemyData> result, List<string> warnings,
        System.Func<BattleEnemyData, int> available)
    {
        if (group == null || group.count <= 0) return;
        var candidates = new List<BattleEnemyData>();
        var seen = new HashSet<string>();
        if (group.enemyIds != null)
            foreach (string id in group.enemyIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!seen.Add(id)) continue;
                if (!lookup.TryGetValue(id, out BattleEnemyData data) || data.prefab == null ||
                    data.rank != rank || data.spawnRole != SpawnRole.Enemy)
                { warnings.Add($"{rank}: 사용할 수 없는 후보 '{id}'를 제외했습니다."); continue; }
                candidates.Add(data);
            }
        if (candidates.Count == 0)
        { warnings.Add($"{rank}: 유효한 후보가 없어 배치를 건너뜁니다."); return; }
        var remaining = new Dictionary<BattleEnemyData, int>();
        foreach (BattleEnemyData data in candidates) remaining[data] = available != null ? available(data) : group.count;
        for (int i = 0; i < group.count; i++)
        {
            candidates.RemoveAll(data => remaining[data] <= 0);
            if (candidates.Count == 0)
            { warnings.Add($"{rank}: 풀 여유분 부족으로 요청 {group.count}마리 중 {i}마리만 배치합니다."); break; }
            BattleEnemyData selected = candidates[Random.Range(0, candidates.Count)];
            remaining[selected]--;
            result.Add(selected);
        }
    }
}
