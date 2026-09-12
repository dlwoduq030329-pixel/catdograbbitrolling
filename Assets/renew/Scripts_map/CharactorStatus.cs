using UnityEngine;
using UnityEngine.Serialization;

/// <summary>캐릭터의 기본 능력치와 기본 전투 자원을 보관합니다.</summary>
public class CharactorStatus : MonoBehaviour
{
    [Header("기본 능력치")]
    [Tooltip("물리 피해와 물리 공격 명중 판정에 사용합니다.")]
    [FormerlySerializedAs("str_st")]
    [SerializeField] private int strength;
    [Tooltip("최대 AP에 사용합니다. DEX 10마다 AP가 1 증가합니다.")]
    [FormerlySerializedAs("dex_st")]
    [SerializeField] private int dexterity;
    [Tooltip("마법 피해와 마법 공격 명중 판정에 사용합니다.")]
    [FormerlySerializedAs("int_st")]
    [SerializeField] private int intelligence;
    [Tooltip("최대 MP에 사용합니다. WIS 10마다 MP가 1 증가합니다.")]
    [FormerlySerializedAs("wis_st")]
    [SerializeField] private int wisdom;
    [Tooltip("상점 할인과 이벤트 성공 확률에 사용합니다.")]
    [FormerlySerializedAs("car_st")]
    [SerializeField] private int charisma;
    [Tooltip("최대 HP에 사용합니다. VIT 1마다 HP가 1 증가합니다.")]
    [FormerlySerializedAs("vit_st")]
    [SerializeField] private int vitality;

    [SerializeField] private int tribeIndex;

    public int STR => strength;
    public int DEX => dexterity;
    public int INT => intelligence;
    public int WIS => wisdom;
    public int VIT => vitality;
    public int CAR => charisma;
    public int CHA => charisma;
    public int TribeIndex => tribeIndex;

    public void InitStatus(int str, int dex, int intStat, int wis, int cha, int vit)
    {
        strength = str;
        dexterity = dex;
        intelligence = intStat;
        wisdom = wis;
        charisma = cha;
        vitality = vit;
    }

    public void TribeSet(int value)
    {
        tribeIndex = value;
    }
}
