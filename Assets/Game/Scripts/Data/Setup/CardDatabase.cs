using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(
    fileName = "CardDatabase",
    menuName = "Card/Card Database"
    )]
public class CardDatabase : ScriptableObject
{
    public List<CardData> cards = new List<CardData>();
}

[System.Serializable]
public class CardData
{
    [SerializeField]
    public Sprite myCardSprite;
    public int index;
    public string name;
    public int cost;
    public string rare;
    public int damage;
    public int heal;
    public string cardInfo;
    public int cardCost;

    public CardData Clone()
    {
        return new CardData
        {
            myCardSprite = this.myCardSprite,
            index = this.index,
            name = this.name,
            cost = this.cost,
            rare = this.rare,
            damage = this.damage,
            heal = this.heal
        };
    }
}