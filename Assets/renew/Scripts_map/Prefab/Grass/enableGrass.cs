using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class enableGrass : MonoBehaviour
{
    [SerializeField]
    GameObject[] grassObj;
    [SerializeField]
    bool count = false;
    // Start is called before the first frame update
    void Start()
    {
        int x = Random.Range(0, grassObj.Length);
        if(count)
        {
            Debug.Log(x.ToString() + "번째 장애물 생성 완료");
            grassObj[x].gameObject.SetActive(true);
        }else
        {
            for (int j = 0; j < x; j++)
            {
                grassObj[j].gameObject.SetActive(true);
            }

        }
    }

    // Update is called once per frame
}
