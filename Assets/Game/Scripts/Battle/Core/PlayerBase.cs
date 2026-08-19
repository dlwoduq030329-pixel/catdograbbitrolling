using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public abstract class PlayerBase : MonoBehaviour
{
    private int str;
    private int wis;
    private int dex;
    private int vit;
    private int tribe;

    public int Str => str;
    public int Wis => wis;
    public int Dex => dex;
    public int Vit => vit;
    public int Tribe => tribe;

    public List<int> deckList = new List<int>(); 

    public void Init()
    {
        str = DataConfig.playerDatas[0];
        wis = DataConfig.playerDatas[1];
        dex = DataConfig.playerDatas[2];
        vit = DataConfig.playerDatas[3];
        tribe = DataConfig.tribe;
    }

    public void DeckInit()
    {
        deckList.Clear();
        deckList = new List<int>(DataConfig.cardData);
    }

    


}
