using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Power1Database",
    menuName = "Power/Power1 Database"
    )]
public class PowerDatabase1 : ScriptableObject
{
    public List<Power1Data> power1 = new List<Power1Data>();
}

[System.Serializable]
public class Power1Data
{
    [SerializeField]
    public Sprite myCardSprite;
    public int index;
    public string title;
    public int strUP;
    public int wisUP;
    public int dexUP;
    public int vitUP;
    public string korName;

    public Power1Data Clone()
    {
        return new Power1Data
        {
            myCardSprite = this.myCardSprite,
            index = this.index,
            title = this.title,
            strUP = this.strUP,
            wisUP = this.wisUP,
            dexUP = this.dexUP,
            vitUP = this.vitUP
        };
    }
}