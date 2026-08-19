using System.Collections.Generic;
using UnityEngine;

/// <summary>공용 대상 선택 시스템에서 구분하는 전투 행동 종류.</summary>
public enum BattleActionType
{
    BasicAttack,
    Card,
    Skill,
    Item
}

/// <summary>
/// 기본 공격이나 카드가 대상 선택 시스템에 전달하는 변경 불가능한 행동 요청 데이터다.
/// </summary>
public sealed class BattleActionRequest
{
    public string DisplayName { get; }
    public BattleActionType ActionType { get; }
    public int RangeTiles { get; }
    public int MPCost { get; }
    public float Power { get; }

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
/// 공격자, 대상, 이동 경로, 행동력 비용을 보관하는 행동 확정 결과 데이터다.
/// </summary>
public sealed class BattleActionResult
{
    public BattleActionRequest Request { get; }
    public GameObject Attacker { get; }
    public GameObject Target { get; }
    public IReadOnlyList<MapInfo> MovementPath { get; }
    public int MovementMPCost { get; }
    public int ActionMPCost { get; }
    public int TotalMPCost => MovementMPCost + ActionMPCost;

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
