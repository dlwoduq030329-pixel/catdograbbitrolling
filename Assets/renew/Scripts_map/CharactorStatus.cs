using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharactorStatus : MonoBehaviour
{
    [SerializeField] int str_st; // 기본 공격력 + 물리 데미지(명중률에 영향을 준다, 데미지 type에 각 state로 계산해야함)
    [SerializeField] int dex_st; // 이동 범위
    [SerializeField] int int_st; // 마법 데미지 증가 + 마법 기본 평타(명중률에 영향을 준다)
    [SerializeField] int wis_st; // 기본 체력 증가
    [SerializeField] int car_st; // mp증가
    [SerializeField] int vit_st; // 상점 할인, 이벤트 성공 확률  
    [SerializeField] int actionpoint_st; // 턴 당 플레이어가 움직일 수 있는 칸
    // [SerializeField] int mp_st; // mp임 카드 사용 및 평타 사용 시 소모함. 하지만 아직 구현 x

    public int STR => str_st;
    public int DEX => dex_st;
    public int INT => int_st;
    public int WIS => wis_st;
    public int VIT => vit_st;
    public int CAR => car_st;
    // public int Mp => mp_st;

    public int ap => actionpoint_st;

    float attackRange;

    int playerHp;

    int tribeIndex;
    public int TribeIndex => tribeIndex;


    public void InitStatus(int st1, int st2, int st3, int st4, int st5, int st6)
    {
        str_st = st1;
        dex_st = st2;
        int_st = st3;
        wis_st = st4;
        car_st = st5;
        vit_st = st6;
    }

    public void TribeSet(int x)
    {
        tribeIndex = x;
    }
}