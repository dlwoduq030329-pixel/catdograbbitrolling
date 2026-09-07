using System;
using UnityEngine;

/// <summary>스테이지 입장 시 배치할 종류와 수량. 생성 결과나 살아 있는 적 수는 저장하지 않는다.</summary>
[CreateAssetMenu(fileName = "StageObjData", menuName = "Renew/전투/스테이지 배치 데이터")]
public sealed class StageObjData : ScriptableObject
{
    [Min(1), Tooltip("1~3은 일반 스테이지, 4는 보스 스테이지의 초기 구성입니다.")]
    public int stageNumber = 1;
    [Tooltip("이 스테이지에서 사용할 적 원본 데이터베이스입니다.")]
    public BattleEnemyDatabase enemyDatabase;
    [InspectorName("일반 적 배치")]
    public StageEnemyGroup normalEnemies = new StageEnemyGroup();
    [InspectorName("정예 적 배치")]
    public StageEnemyGroup eliteEnemies = new StageEnemyGroup();
    [InspectorName("보스 배치")]
    public StageEnemyGroup bossEnemies = new StageEnemyGroup();
    [Min(0), Tooltip("시작 타일 주변 사각형 안전 구역의 반경(칸). 1이면 중심 포함 3×3입니다.")]
    public int safeRadiusTiles = 1;

}

[Serializable]
public sealed class StageEnemyGroup
{
    [Min(0), Tooltip("이 등급에서 생성할 총 마릿수입니다. 후보별 수량이 아닙니다.")]
    public int count;
    [Tooltip("EnemyData의 고정 id입니다. 목록 순서를 바꿔도 연결이 유지되며, 후보별 동일 확률로 중복 추첨합니다.")]
    public string[] enemyIds = Array.Empty<string>();
}
