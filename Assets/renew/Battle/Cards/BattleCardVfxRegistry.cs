using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Battle 전용 카드 코드와 레거시 VFX 프리팹의 연결 정보다.
/// 플레이어 프리팹에 레거시 PlayerSkills를 다시 붙이지 않고 연출 자산만 재사용한다.
/// </summary>
[CreateAssetMenu(fileName = "BattleCardVfxRegistry", menuName = "Renew/Battle/Card VFX Registry")]
public sealed class BattleCardVfxRegistry : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        public string cardCode;
        public GameObject prefab;
    }

    private const string ResourcePath = "Battle/CardVfx/BattleCardVfxRegistry";

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public static BattleCardVfxRegistry Load()
    {
        return Resources.Load<BattleCardVfxRegistry>(ResourcePath);
    }

    public GameObject Find(string cardCode)
    {
        if (string.IsNullOrWhiteSpace(cardCode)) return null;
        Entry entry = entries.Find(item => item != null &&
            string.Equals(item.cardCode, cardCode, StringComparison.OrdinalIgnoreCase));
        return entry != null ? entry.prefab : null;
    }
}
