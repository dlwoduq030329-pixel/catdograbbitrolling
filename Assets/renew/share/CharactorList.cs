using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class CharactorList : MonoBehaviour
{
    [SerializeField]
    DrawState drawState;

    private void OnEnable()
    {
        Debug.Log(DataConfig.playerDatas[0] + DataConfig.playerDatas[1] + DataConfig.playerDatas[2] + DataConfig.playerDatas[3]);
        drawState.Refresh();

    }
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
       // drawState.SetVerticesDirty();

    }
}
