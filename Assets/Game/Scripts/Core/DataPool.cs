using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class DataPool : MonoBehaviour
{
    public static DataPool Instance;

    [SerializeField]
    public CardDatabase cardDatabase;
    [SerializeField]
    public EquipDatabase equipDatabase;
    [SerializeField]
    public PowerDatabase1 powerDatabase1;
    [SerializeField]
    public PowerDatabase2 powerDatabase2;
    [SerializeField]
    public PowerDatabase3 powerDatabase3;
    [SerializeField]
    public StoryData storydata;

    Vector3 nowpos;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    public void LoadPos(Vector3 pos)
    {
        nowpos = pos;
    }

    public Vector3 returnpos()
    {
        return nowpos;
    }
}
