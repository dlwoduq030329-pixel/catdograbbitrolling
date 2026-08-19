using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharactorStatus : MonoBehaviour
{
    [SerializeField] int str_st;
    [SerializeField] int dex_st;
    [SerializeField] int int_st;
    [SerializeField] int wis_st;
    [SerializeField] int car_st;
    [SerializeField] int vit_st;

    public int STR => str_st;
    public int DEX => dex_st;
    public int INT => int_st;
    public int WIS => wis_st;
    public int VIT => vit_st;
    public int CAR => car_st;

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