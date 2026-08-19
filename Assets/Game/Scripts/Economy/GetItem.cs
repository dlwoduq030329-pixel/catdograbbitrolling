using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GetItem : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler
{
    [SerializeField]
    GameObject CardInfo;
    Treasure tresure;

    int index;
    RewardState state;
    string CardString;
    public void OnPointerEnter(PointerEventData eventData)
    {
        CardInfo.gameObject.SetActive(true);

        if(state ==RewardState.Card)
        {
            CardInfo.gameObject.GetComponent<CardInfo>().CardInfoSet(index);
        }

        if(state == RewardState.Equipment)
        {
            CardInfo.gameObject.GetComponent<CardInfo>().CardInfoSet(index,CardString);

        }
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        CardInfo.gameObject.SetActive(false);

    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (state == RewardState.Equipment) return; 
        DataConfig.AddDic(index,1);
        GameManagerInMain.Instance.activeRoll(rollUseage.Move);
        //È×µæ ÀÌÆåÆ® Àç»ý
        CardInfo.gameObject.SetActive(false);
        GetComponentInParent<Treasure>().Close();
    }

    private void Awake()
    {
        
    }

    public void Init(RewardState st,int cardIndex)
    {
        state = st;
        index = cardIndex;
    }

    public void Init(RewardState st,int cardIndex, string temp)
    {
        state = st;
        index = cardIndex;
        CardString = temp;
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
