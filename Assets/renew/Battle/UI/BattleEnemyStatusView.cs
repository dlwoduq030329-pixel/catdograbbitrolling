using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy에 연결된 공용 상태이상 저장소(BattleStatusEffects)의 변경 이벤트를 구독하고, 향후 아이콘 UI가
/// 사용할 `상태 종류·남은 턴·중첩` 목록으로 정리해 보관한다.
/// 2026-09-05: 기절·속박만 따로 관리하던 BattleEnemyControlState는 폐지됐다 — Player와 완전히 같은
/// BattleStatusEffects 하나만 구독하면 기절·속박을 포함한 모든 상태이상이 이미 그 목록에 들어있다.
/// 현재는 텍스트나 UI 오브젝트를 생성하지 않으며, 사용자가 상태 아이콘 프리팹을 준비한 뒤
/// <see cref="CurrentStatusEntries"/>를 순회해 각 아이콘에 표시 데이터를 전달하도록 확장한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyStatusView : MonoBehaviour
{
    private readonly List<BattleStatusDisplayEntry> currentStatusEntries =
        new List<BattleStatusDisplayEntry>();

    private BattleStatusEffects boundStatusEffects;

    /// <summary>
    /// 마지막 상태 변경 신호에서 다시 구성한 Enemy의 현재 상태 목록이다.
    /// 외부 UI는 목록을 수정하지 않고 순회해 상태 종류에 맞는 이미지와 남은 턴을 표시한다.
    /// </summary>
    public IReadOnlyList<BattleStatusDisplayEntry> CurrentStatusEntries => currentStatusEntries;

    /// <summary>
    /// 상태 목록이 다시 구성된 뒤 발생한다. 향후 아이콘 컨테이너가 이 이벤트를 구독해 필요한 슬롯만 갱신한다.
    /// </summary>
    public event Action StatusEntriesChanged;

    /// <summary>같은 Enemy에 직접 연결된 공용 상태이상 저장소를 최초로 가져온다.</summary>
    private void Awake()
    {
        boundStatusEffects = GetComponent<BattleStatusEffects>();
    }

    /// <summary>컴포넌트가 활성화되면 공용 상태이상 변경 이벤트를 연결하고 현재 목록을 즉시 구성한다.</summary>
    private void OnEnable()
    {
        BindStatusSource(
            boundStatusEffects != null
                ? boundStatusEffects
                : GetComponent<BattleStatusEffects>());
    }

    /// <summary>비활성화된 Enemy View가 계속 상태 변경 신호를 받지 않도록 구독을 해제한다.</summary>
    private void OnDisable()
    {
        if (boundStatusEffects != null)
        {
            boundStatusEffects.Changed -= OnBattleStatusEffectsChanged;
        }
    }

    /// <summary>
    /// 이 Enemy의 공용 상태이상 저장소를 연결한다. 기존 저장소 구독을 먼저 해제한 뒤 새 저장소를 구독하고,
    /// 현재 상태이상(기절·속박 포함)을 표시 목록으로 즉시 다시 구성한다.
    /// </summary>
    public void BindStatusSource(BattleStatusEffects statusSource)
    {
        if (boundStatusEffects != null)
        {
            boundStatusEffects.Changed -= OnBattleStatusEffectsChanged;
        }

        boundStatusEffects = statusSource;
        if (boundStatusEffects != null)
        {
            boundStatusEffects.Changed -= OnBattleStatusEffectsChanged;
            boundStatusEffects.Changed += OnBattleStatusEffectsChanged;
        }

        RebuildCurrentStatusEntries();
    }

    /// <summary>상태이상 목록이 바뀌면 표시 목록을 다시 만든다.</summary>
    private void OnBattleStatusEffectsChanged(BattleStatusEffects changedStatusEffects)
    {
        RebuildCurrentStatusEntries();
    }

    /// <summary>
    /// BattleStatusEffects의 현재 목록을 그대로 표시 목록으로 복사한다(기절·속박도 이미 포함되어 있어
    /// 별도로 덧붙일 필요가 없다). 목록 구성이 끝나면 향후 아이콘 UI가 다시 그릴 수 있도록
    /// StatusEntriesChanged 이벤트를 발생시킨다.
    /// </summary>
    private void RebuildCurrentStatusEntries()
    {
        if (boundStatusEffects != null)
        {
            boundStatusEffects.CopyActiveStatusesTo(currentStatusEntries);
        }
        else
        {
            currentStatusEntries.Clear();
        }

        StatusEntriesChanged?.Invoke();
    }
}
