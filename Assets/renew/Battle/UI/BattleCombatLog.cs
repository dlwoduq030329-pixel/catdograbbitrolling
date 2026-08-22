using System;
using System.Collections.Generic;

/// <summary>
/// 턴 시작, 피해, 회복처럼 플레이어에게 보여 줄 전투 기록 문자열을 실행 중 메모리에 보관한다.
/// 전투 로직은 <see cref="AddEntry"/>로 기록만 전달하고, UI는 <see cref="LogEntriesChanged"/>를
/// 구독해 원하는 형태로 다시 표시하므로 전투 계산 코드와 최종 로그 UI 배치를 분리할 수 있다.
/// </summary>
public static class BattleCombatLog
{
    private static readonly List<string> battleLogEntries = new List<string>();

    /// <summary>
    /// 현재 전투에서 지금까지 추가된 로그를 발생 순서대로 제공한다.
    /// 외부 코드가 목록을 직접 수정하지 못하도록 읽기 전용 인터페이스로 공개한다.
    /// </summary>
    public static IReadOnlyList<string> LogEntries => battleLogEntries;

    /// <summary>
    /// 로그가 추가되거나 전체 초기화되어 화면을 다시 그려야 할 때 발생한다.
    /// 현재 <see cref="BattleCombatLogView"/>가 이 이벤트를 구독해 스크롤 텍스트를 갱신한다.
    /// </summary>
    public static event Action LogEntriesChanged;

    /// <summary>
    /// 이전 전투의 모든 로그를 제거하고 구독 중인 UI에 빈 목록을 다시 표시하라고 알린다.
    /// BattleGameManager가 새 전투 상태를 초기화할 때 호출한다.
    /// </summary>
    public static void ClearAllEntries()
    {
        battleLogEntries.Clear();
        LogEntriesChanged?.Invoke();
    }

    /// <summary>
    /// 전투 흐름에서 전달받은 한 줄의 기록을 목록 마지막에 추가하고 UI 갱신 이벤트를 발생시킨다.
    /// null, 빈 문자열 또는 공백뿐인 문자열은 화면에 의미 없는 줄을 만들지 않도록 무시한다.
    /// </summary>
    public static void AddEntry(string combatMessage)
    {
        if (string.IsNullOrWhiteSpace(combatMessage))
        {
            return;
        }

        battleLogEntries.Add(combatMessage);
        LogEntriesChanged?.Invoke();
    }
}
