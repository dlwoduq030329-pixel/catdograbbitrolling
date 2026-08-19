using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitPool : MonoBehaviour
{
    private static UnitPool instance;
    public static UnitPool Instance => instance;

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
    }
    [SerializeField]
    public UnitDataBuilder unit;
    [SerializeField]
    public GameObject[] playerPrefabs;
}
