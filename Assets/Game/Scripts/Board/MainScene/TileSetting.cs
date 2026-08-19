using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TileState { 
None,
Start,
Battle,
Store,
Event,
Discovery
}

public class TileSetting : MonoBehaviour
{
    public TileState mystate;

    private void OnEnable()
    {
        if (mystate == TileState.Start) return;
        mystate = (TileState)Random.Range(1, 5);
    }


    public void SetState()
    {
        switch(mystate)
        {
            case TileState.Start:
                break;
            case TileState.Battle:
                break;
            case TileState.Store:
                break;
            case TileState.Event:
                break;
            case TileState.Discovery:
                break;
        }
    }
}
