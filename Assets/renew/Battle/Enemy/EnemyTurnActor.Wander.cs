using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyTurnActor의 "감지 대상이 없을 때 배회" 부분만 따로 뗀 partial 조각이다. TakeTurn(본체는
/// EnemyTurnActor.cs)이 대상을 못 찾았을 때 이 파일의 TryWanderStep을 호출한다. 코드 자체는 원래
/// EnemyTurnActor.cs에 있던 것을 파일만 분리했을 뿐 로직·필드 접근 방식은 그대로다(C# partial class로
/// 한 클래스를 여러 파일에 나눠 담은 것 — 컴파일되면 완전히 같은 클래스로 합쳐진다).
/// </summary>
public partial class EnemyTurnActor
{
    /// <summary>배회 중 원래 자리를 기억해 wanderRadiusTiles 밖으로 못 벗어나게 하는 기준점.
    /// 첫 배회 시도 시점의 타일로 한 번만 설정되고 이후 바뀌지 않는다(스폰 위치 근사).</summary>
    private MapInfo homeTile;

    /// <summary>attackRangeTiles/attackDamageType과 같은 패턴으로, ConfigureFromData가 이미 복사해 둔
    /// idleBehavior 필드를 그대로 반환한다(= 원본 BattleEnemyData.idleBehavior와 같은 값이면서, Play 모드
    /// Inspector의 "Movement Type"에서 지금 이 Enemy가 정적/배회 중 뭔지 바로 확인할 수 있다).</summary>
    private EnemyIdleBehavior GetIdleBehavior()
    {
        return idleBehavior;
    }

    /// <summary>
    /// idleBehavior가 Wander이고 이번 턴 감지된 Player가 없을 때 호출된다. BattleEnemyData의
    /// wanderChance 확률로 이번 턴 배회 여부를 먼저 굴리고, 성공하면 wanderTilesPerTurn 만큼
    /// 한 칸씩 인접 타일로 이동한다. 이동 가능(IsWalkable), 다른 Enemy 비점유, homeTile(첫 배회 시점
    /// 위치, 스폰 지점 근사) 기준 wanderRadiusTiles 이내라는 세 조건을 모두 만족하는 타일만 후보로 삼는다.
    /// 매 스텝을 완전히 독립적으로 무작위 선택하면(2026-09-04 초기 구현) 방금 온 칸으로 바로 되돌아가는
    /// 왔다갔다 지그재그가 자주 나와 "멍청해 보인다"는 피드백을 받아, 직전에 있던 칸(previousTile)은
    /// 다른 후보가 있는 한 이번 스텝 후보에서 제외해 최소한 제자리 왕복은 피하게 했다.
    /// 목표를 향한 추격이 아니라 그냥 주변을 서성이는 용도라 공격 사거리 개념이 없고, 매 칸마다
    /// 후보가 없거나 MP가 부족해지면 그 자리에서 조용히 배회를 멈춘다.
    /// </summary>
    private IEnumerator TryWanderStep(BattleCameraRig cameraRig)
    {
        BattleEnemyData data = runtimeData != null ? runtimeData.Data : null;
        float wanderChance = data != null ? data.wanderChance : 0.6f;
        int wanderRadiusTiles = data != null ? data.wanderRadiusTiles : 3;
        int wanderTilesPerTurn = Mathf.Max(1, data != null ? data.wanderTilesPerTurn : 1);

        if (UnityEngine.Random.value > wanderChance)
        {
            // 배회 확률 실패 — 이번 턴은 그냥 가만히 있는다.
            yield break;
        }

        ResolveBattleDataPool();
        IReadOnlyList<MapInfo> mapTiles = mapContext.GetMapTiles(battleDataPool);
        MapInfo currentTile = MapPathfinder.FindClosestTile(transform.position, mapTiles);
        if (currentTile == null)
        {
            yield break;
        }

        // homeTile은 처음 배회를 시도한 그 위치로 한 번만 고정한다(스폰 지점 근사). 이후 계속 이 기준으로
        // wanderRadiusTiles를 재는데, 정확한 스폰 타일을 별도로 기억하지 않는 현재 구조에서 가장 단순하고
        // 안전한 근사치다(스폰 직후 첫 배회 시도 위치 = 스폰 위치와 사실상 같다).
        if (homeTile == null)
        {
            homeTile = currentTile;
        }

        int movedTiles = 0;
        MapInfo previousTile = null;
        for (int step = 0; step < wanderTilesPerTurn; step++)
        {
            HashSet<MapInfo> occupiedTiles = mapContext.FindOtherEnemyTiles(battleDataPool, this, mapTiles);

            List<MapInfo> wanderCandidates = new List<MapInfo>(4);
            List<MapInfo> wanderCandidatesExcludingBacktrack = new List<MapInfo>(4);
            MapInfo[] neighbours = { currentTile.Up, currentTile.Down, currentTile.Left, currentTile.Right };
            foreach (MapInfo neighbour in neighbours)
            {
                if (neighbour == null || !neighbour.IsWalkable || occupiedTiles.Contains(neighbour))
                {
                    continue;
                }

                int distanceFromHome =
                    Mathf.Abs(neighbour.Index.x - homeTile.Index.x) +
                    Mathf.Abs(neighbour.Index.y - homeTile.Index.y);
                if (distanceFromHome > wanderRadiusTiles)
                {
                    continue;
                }

                wanderCandidates.Add(neighbour);
                if (neighbour != previousTile)
                {
                    wanderCandidatesExcludingBacktrack.Add(neighbour);
                }
            }

            // 방금 있던 칸으로 되돌아가는 선택지는, 그것 말고 갈 곳이 아예 없을 때(막다른 길)만 허용한다.
            List<MapInfo> effectiveCandidates =
                wanderCandidatesExcludingBacktrack.Count > 0 ? wanderCandidatesExcludingBacktrack : wanderCandidates;

            if (effectiveCandidates.Count == 0)
            {
                break;
            }

            int moveCostPerTile = GetMoveCostPerTile();
            if (characterMP == null || characterMP.CurrentMP < Mathf.Max(1, moveCostPerTile))
            {
                break;
            }

            MapInfo destinationTile = effectiveCandidates[UnityEngine.Random.Range(0, effectiveCandidates.Count)];
            yield return BeginActionFocus(cameraRig);
            yield return actionExecutor.MoveToSingleTile(destinationTile, currentTile, moveCostPerTile);
            movedTiles++;
            previousTile = currentTile;
            currentTile = destinationTile;
        }

        if (movedTiles > 0 && afterActionSeconds > 0f)
            yield return new WaitForSecondsRealtime(afterActionSeconds);
    }
}
