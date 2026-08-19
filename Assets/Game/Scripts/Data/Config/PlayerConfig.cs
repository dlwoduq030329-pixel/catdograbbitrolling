using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
public enum Charactor
{
    Null,
    OWL,
    DOG,
    RABBIT,
    BEAR
}

public static class PlayerConfig
{
    public static string Name; 

    public static int str = 0;
    public static int wis = 0;
    public static int dex = 0;
    public static int vit = 0;

    public static Charactor myCharactor = Charactor.Null;
    public static int rightWeapon = 0;
    public static int leftWeapon = 0;
    public static int head = 0;
    public static int body = 0;
    public static string playerName;
    public static int setIndex = 0;

    public static string title;
    public static string tribe = string.Empty;
    public static int playerJobIndex = 0;
    //

    public static void ApplyToPhoton()
    {
        Hashtable props = new Hashtable
        {
            { "Name", Name },
            { "str", str },
            { "wis", wis },
            { "dex", dex },
            { "vit", vit },
            { "Char", (int)myCharactor },
            { "RW", rightWeapon },
            { "LW", leftWeapon },
            { "Head", head },
            { "Body", body },
            { "playerName", playerName },
            { "SetIndex", setIndex },
            { "title", title },            
            { "tribe", tribe },
            { "playerJobIndex",playerJobIndex}




        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }
    public static void LoadFromPhoton(Player player)
    {
        var p = player.CustomProperties;

        if (p.TryGetValue("Name", out var n)) Name = (string)n;
        if (p.TryGetValue("str", out var s)) str = (int)s;
        if (p.TryGetValue("wis", out var w)) wis = (int)w;
        if (p.TryGetValue("dex", out var d)) dex = (int)d;
        if (p.TryGetValue("vit", out var v)) vit = (int)v;
        if (p.TryGetValue("Char", out var c)) myCharactor = (Charactor)(int)c;
        if (p.TryGetValue("RW", out var rw)) rightWeapon = (int)rw;
        if (p.TryGetValue("LW", out var lw)) leftWeapon = (int)lw;
        if (p.TryGetValue("Head", out var h)) head = (int)h;
        if (p.TryGetValue("Body", out var b)) body = (int)b;
        if (p.TryGetValue("PlayerName", out var pn)) playerName = (string)pn;
        if (p.TryGetValue("SetIndex", out var si)) setIndex = (int)si;
        if (p.TryGetValue("title", out var t)) title = (string)t;
        if (p.TryGetValue("tribe", out var tr)) tribe = (string)tr;
        if (p.TryGetValue("playerJonIndex", out var pJI)) playerJobIndex = (int)pJI;

    }

}
