using System.Collections.Generic;
using System.Globalization;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public enum weaponSt
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum WeaponKind
{
    Hand,
    Body,
    Head,
    TwoHand
}

[CreateAssetMenu(
    fileName = "EquipDatabase",
    menuName = "Equip/Equip Database"
    )]
public class EquipDatabase : ScriptableObject
{
    public List<EquipData> equip = new List<EquipData>();
}

[System.Serializable]
public class EquipData
{
    [SerializeField]
    public Sprite myEquipSprite;
    public string cardname;
    public float attackRange;
    public float attackSpeed;
    public int stroffset;
    public int dexoffset;
    public int wisoffset;
    public int vitoffset;
    public int weaponIndex;
    public weaponSt weapon;
    public WeaponKind weaponKind;
    public int cost;
    public GameObject weaponPrefab;
    public GameObject weaponPrefab2;

    public EquipData(EquipData other)
    {
        myEquipSprite = other.myEquipSprite;
        cardname = other.cardname;
        attackRange = other.attackRange;
        attackSpeed = other.attackSpeed;

        stroffset = other.stroffset;
        dexoffset = other.dexoffset;
        wisoffset = other.wisoffset;
        vitoffset = other.vitoffset;

        weaponIndex = other.weaponIndex;
        weapon = other.weapon;
        weaponKind = other.weaponKind;
        cost = other.cost;
        weaponPrefab = other.weaponPrefab;
        weaponPrefab2 = other.weaponPrefab2;
    }

    public EquipData()
    {
       
    }



    public EquipData Clone()
    {
        return new EquipData(this);
    }
     
}
