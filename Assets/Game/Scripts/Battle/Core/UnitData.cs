using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum unitState
{
    totem,
    enemyunit,
    elite,
    boss
}

public enum attackState
{
    melee,
    range
}

[System.Serializable]
public class UnitData
{
    public GameObject enemyPrefab;
    public unitState unitstate;
    public attackState attackstate;
    public string unitName;
    public float attackRange;
    public float attackDamage;
    public float enemyHP;
    public float attackSpeed;
    public float skillCool;
}
