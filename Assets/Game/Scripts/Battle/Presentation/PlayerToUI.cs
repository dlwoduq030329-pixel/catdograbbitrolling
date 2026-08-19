using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerToUI : MonoBehaviour
{
    [SerializeField]
    GameObject Store;
    [SerializeField]
    GameObject Event_;
    [SerializeField]
    GameObject discovery;
    [SerializeField]
    TextMeshProUGUI myGold;
    private int eventIndex;
    public int EventIndex => eventIndex;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateMyGold()
    {
        myGold.text = "Gold : " + DataConfig.playerMoney.ToString();
    }

    public void OpenStore()
    {
       
        Store.gameObject.SetActive(true);
    }

    public void CloseStore()
    {
        Store.gameObject.SetActive(false);

    }
    public void OpenEvent()
    {
       // Event_.GetComponent<EventSet>().StorySet();
        Event_.gameObject.SetActive(true);
        //이벤트 받아오는 코드...
        //필요 스텟 적용.

        //Button[] eventBTN = Event_.GetComponentsInChildren<Button>(true);
        //eventBTN[1].onClick.AddListener(GetState);
        //eventBTN[0].onClick.Invoke();

        
    }
    public void OpenEventGetState()
    {
        
    }

    public void OpenDiscovery()
    {
        
        discovery.gameObject.SetActive(true);
        discovery.gameObject.GetComponent<Treasure>().Open();
    }

    public void GetState()
    {
        GameManagerInMain.Instance.activeRoll(rollUseage.Event);
        Button[] eventBTN = Event_.GetComponentsInChildren<Button>(true);
        eventBTN[1].onClick.RemoveAllListeners();

        Event_.gameObject.SetActive(false);
    }

 

}
