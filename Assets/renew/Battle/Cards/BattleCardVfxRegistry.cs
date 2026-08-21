using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Battle 전용 카드 코드와 레거시 VFX 프리팹의 연결 정보다.
/// 플레이어 프리팹에 레거시 PlayerSkills를 다시 붙이지 않고 연출 자산만 재사용한다.
/// 현재 Resources 경로와 문자열 cardCode에 의존하므로 카드 데이터의 직접 VFX Prefab 참조로 교체 후 삭제한다.
/// </summary>
[CreateAssetMenu(fileName = "BattleCardVfxRegistry", menuName = "Renew/Battle/Card VFX Registry")]
public sealed class BattleCardVfxRegistry : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        /// <summary>Legacy 카드 연출을 찾는 영문 문자열 키.</summary>
        public string cardCode;
        /// <summary>해당 카드가 실행된 뒤 생성할 시각 연출 Prefab.</summary>
        public GameObject prefab;
    }

    private const string ResourcePath = "Battle/CardVfx/BattleCardVfxRegistry";

    [SerializeField] private List<Entry> entries = new List<Entry>();

    /// <summary>고정 Resources 경로에서 Registry 에셋을 불러온다. 직접 참조 전환 전까지만 사용하는 호환 API다.</summary>
    public static BattleCardVfxRegistry Load()
    {
        return Resources.Load<BattleCardVfxRegistry>(ResourcePath);
    }

    /// <summary>대소문자를 무시하고 cardCode가 일치하는 첫 VFX Prefab을 반환한다.</summary>
    public GameObject Find(string cardCode)
    {
        if (string.IsNullOrWhiteSpace(cardCode)) return null;
        Entry entry = entries.Find(item => item != null &&
            string.Equals(item.cardCode, cardCode, StringComparison.OrdinalIgnoreCase));
        return entry != null ? entry.prefab : null;
    }
}
