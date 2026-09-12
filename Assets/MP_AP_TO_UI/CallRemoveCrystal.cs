using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CallRemoveCrystal : MonoBehaviour
{
    [SerializeField]
    int crystalIndex;

    public void CallRemove()
    {
        ShowMpAPToUI ui = GetComponentInParent<ShowMpAPToUI>();
        ui.UseMPaniKey(crystalIndex);
    }

    public void CallAPRemove()
    {
        ShowMpAPToUI ui = GetComponentInParent<ShowMpAPToUI>();
        ui.UseAPaniKey(crystalIndex);
    }
}
