using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Power3Database",
    menuName = "Power/Power3 Database"
)]
public class PowerDatabase3 : ScriptableObject
{
    public List<Power3Data> power3 = new List<Power3Data>();
}

[System.Serializable]
public class Power3Data
{
    [SerializeField]
    public Sprite myCardSprite;
    public int index;
    public string title;
    public int strUp;
    public int wisUP;
    public int dexUP;
    public int vitUP;
    public string activeFuncName;

    public Power3Data Clone()
    {
        return new Power3Data
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