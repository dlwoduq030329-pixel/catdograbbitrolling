using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Power2Database",
    menuName = "Power/Power2 Database"
)]
public class PowerDatabase2 : ScriptableObject
{
    public List<Power2Data> power2 = new List<Power2Data>();
}

[System.Serializable]
public class Power2Data
{
    [SerializeField]
    public Sprite myCardSprite;
    public int index;
    public string title;
    public int strUp;
    public int wisUP;
    public int dexUP;
    public int vitUP;
    public int addCardIndex;

    public Power2Data Clone()
    {
        return new Power2Data
        {
            myCardSprite = this.myCardSprite,
            index = this.index,
            title = this.title,
            strUp = this.strUp,
            wisUP = this.wisUP,
            dexUP = this.dexUP,
            vitUP = this.vitUP
        };
    }

}