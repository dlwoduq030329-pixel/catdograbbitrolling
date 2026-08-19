using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum nodeState
{
    None,
    start,
    battle,
    store,
    discovery,
    gameevent
}

public class Node : MonoBehaviour
{
    [SerializeField]
    public int id;
    [SerializeField]
    public int NextTileid;

    public nodeState state = nodeState.None;

    public void Awake()
    {
        string temp = this.gameObject.name;
        temp = temp.Substring(0, 2);
        
        id = int.Parse(temp) -1;
        NextTileid = id + 1;
        if(NextTileid>71)
        {
            NextTileid = 0;
        }
    }

    public void OnEnable()
    {
       
    }

    public void SetState(nodeState now)
    {
        //state = now;
    }
}
