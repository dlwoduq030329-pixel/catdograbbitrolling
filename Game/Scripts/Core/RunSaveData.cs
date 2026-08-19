using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RunSaveData
{
    public int saveVersion = 2;
    public int[] playerStats;
    public int tribe;
    public List<int> deck = new List<int>();
    public List<int> cardIds = new List<int>();
    public List<int> cardCounts = new List<int>();
    public int boardPosition;
    public string playerName;
    public int difficulty;
    public int enemyCount;
    public int gold;
    public int turnCount;
    public int stage;
    public int turn;
    public int titleIndex;
    public int jobIndex;
    public Quaternion boardRotation;
    public EquipSaveData left;
    public EquipSaveData right;
    public EquipSaveData head;
    public EquipSaveData body;
}

[Serializable]
public class EquipSaveData
{
    public int index;
    public int rarity;
    public int str;
    public int wis;
    public int dex;
    public int vit;
    public int cost;

    public static EquipSaveData FromEquip(EquipData equip)
    {
        if (equip == null) return null;
        return new EquipSaveData
        {
            index = equip.weaponIndex,
            rarity = (int)equip.weapon,
            str = equip.stroffset,
            wis = equip.wisoffset,
            dex = equip.dexoffset,
            vit = equip.vitoffset,
            cost = equip.cost
        };
    }
}
