using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 선택한 행동을 전투 시스템이 구분할 때 사용하는 종류다.
/// 요청을 받은 컨트롤러는 이 값을 기준으로 기본 공격과 카드 등 서로 다른 실행 흐름을 구별한다.
/// </summary>
public enum BattleActionType
{
    /// <summary>무기나 STR을 기반으로 수행하는 플레이어의 기본 공격.</summary>
    BasicAttack,

    /// <summary>플레이어 덱에서 뽑은 카드를 사용하는 행동.</summary>
    Card,

    /// <summary>추후 별도의 스킬 시스템을 연결하기 위해 확보한 행동 종류.</summary>
    Skill,

    /// <summary>추후 전투 중 소비 아이템을 연결하기 위해 확보한 행동 종류.</summary>
    Item
}

/// <summary>
/// 플레이어가 선택한 행동의 기본 조건을 대상 선택 및 실행 시스템으로 전달하는 읽기 전용 요청 데이터다.
/// 이 단계에서는 아직 공격 대상과 이동 경로가 확정되지 않았으므로 행동 이름, 종류, 사거리, 비용, 위력만 보관한다.
/// 기본 공격은 <c>BattleBasicAttackController</c>가 만들고, 카드는 <c>BattleCardConnector</c>가 카드 데이터를 변환해 만든다.
/// 이후 대상과 경로가 확정되면 이 요청은 <see cref="BattleActionResult"/> 안에 포함되어 확정 결과로 전달된다.
/// </summary>
public sealed class BattleActionRequest
{
    /// <summary>선택 UI와 전투 로그에서 플레이어에게 보여 줄 행동 이름.</summary>
    public string DisplayName { get; }

    /// <summary>기본 공격, 카드, 스킬, 아이템 중 어느 실행 흐름을 사용할지 구분하는 값.</summary>
    public BattleActionType ActionType { get; }

    /// <summary>대상을 선택할 수 있는 타일 기준 최대 사거리. 최소 1칸으로 보정된다.</summary>
    public int RangeTiles { get; }

    /// <summary>행동 자체를 실행할 때 필요한 MP. 대상에게 접근하는 이동 MP는 포함하지 않는다.</summary>
    public int MPCost { get; }

    /// <summary>행동이 전달하는 기본 위력. 실제 피해·회복량은 실행 파이프라인의 보정 계산을 거쳐 달라질 수 있다.</summary>
    public float Power { get; }

    /// <summary>
    /// 선택된 행동의 공통 실행 조건을 하나의 요청으로 묶는다.
    /// 잘못된 외부 데이터가 들어와도 선택 시스템이 음수 사거리·비용·위력을 처리하지 않도록 안전 범위로 보정한다.
    /// </summary>
    public BattleActionRequest(
        string displayName,
        BattleActionType actionType,
        int rangeTiles,
        int mpCost,
        float power)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "행동" : displayName;
        ActionType = actionType;
        RangeTiles = Mathf.Max(1, rangeTiles);
        MPCost = Mathf.Max(0, mpCost);
        Power = Mathf.Max(0f, power);
    }
}

/// <summary>
/// 대상 선택과 비용 계산이 끝난 뒤, 실제로 확정된 행동 내용을 호출자에게 돌려주는 결과 데이터다.
/// 요청 단계에는 없던 공격자, 최종 대상, 접근 경로와 실제 소비 비용을 함께 보관한다.
/// 기본 공격·카드 컨트롤러가 이 결과를 <c>Confirmed</c> 이벤트로 전달하면
/// <c>BattlePlayerActionController</c>가 받아 플레이어 행동 완료와 후속 UI 갱신을 처리한다.
/// 피해량 계산 결과인 <c>BattleDamageResult</c>와 달리, 이 타입은 '어떤 플레이어 행동이 확정됐는가'를 표현한다.
/// </summary>
public sealed class BattleActionResult
{
    /// <summary>이번에 확정된 행동의 이름, 종류, 기본 사거리·비용·위력.</summary>
    public BattleActionRequest Request { get; }

    /// <summary>행동을 시작한 플레이어 또는 전투 유닛.</summary>
    public GameObject Attacker { get; }

    /// <summary>플레이어가 최종 확정한 대상. 단일 대상이 없는 행동에서는 null일 수 있다.</summary>
    public GameObject Target { get; }

    /// <summary>행동 사거리를 확보하기 위해 공격자가 이동한 타일 순서. 이동하지 않았다면 빈 목록일 수 있다.</summary>
    public IReadOnlyList<MapInfo> MovementPath { get; }

    /// <summary>확정된 접근 경로를 이동하는 데 소비한 MP.</summary>
    public int MovementMPCost { get; }

    /// <summary>기본 공격이나 카드 효과 자체를 실행하는 데 소비한 MP.</summary>
    public int ActionMPCost { get; }

    /// <summary>이동 비용과 행동 비용을 합친 이번 행동의 전체 MP 소비량.</summary>
    public int TotalMPCost => MovementMPCost + ActionMPCost;

    /// <summary>
    /// 대상 선택과 비용 검증을 통과한 행동 정보를 확정 결과로 묶는다.
    /// 비용은 음수가 되지 않도록 보정하지만, 요청·공격자·대상·경로의 유효성은 결과를 만드는 컨트롤러가 책임진다.
    /// </summary>
    public BattleActionResult(
        BattleActionRequest request,
        GameObject attacker,
        GameObject target,
        IReadOnlyList<MapInfo> movementPath,
        int movementMPCost,
        int actionMPCost)
    {
        Request = request;
        Attacker = attacker;
        Target = target;
        MovementPath = movementPath;
        MovementMPCost = Mathf.Max(0, movementMPCost);
        ActionMPCost = Mathf.Max(0, actionMPCost);
    }
}
