using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class ResultPopup : MonoBehaviour
{

    [SerializeField]
    TextMeshProUGUI goldText;
    [SerializeField]
    TextMeshProUGUI timeText;
    [SerializeField]
    Sprite[] tribeSp;
    [SerializeField]
    Image tribeIMG;
    [SerializeField]
    TextMeshProUGUI damText;

    public void Init(float getGold,float battleTime,int tribe)
    {
        goldText.text = getGold.ToString() + "G";
        int temp =(int)battleTime;
        timeText.text = temp.ToString() + "초";
        tribeIMG.sprite = tribeSp[tribe];
        damText.text ="피해량 : " + (UnitPool.Instance.unit.unitDatas[DataConfig.hard].enemyHP * DataConfig.count).ToString();

    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
