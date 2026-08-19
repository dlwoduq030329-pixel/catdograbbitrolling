using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
//using static UnityEngine.Rendering.DebugUI;
public class UIManager : MonoBehaviour
{

    [SerializeField]
    Image ChoosePowerUp;
    [SerializeField]
    Button[] buttons;
    [SerializeField]
    GameObject rollbtn;
    [SerializeField]
    Image blackIMG;
    public static UIManager Instance;
    [SerializeField]
    TextMeshProUGUI turnText;
    int turnIndex = 0;
    

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }



    // Start is called before the first frame update
    void Start()
    {
        blackIMG.gameObject.SetActive(true);
        //RefreshDiceButton();
        //TurnManager.Instance.OnTurnChanged += OnTurnChanged;
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    public void disableBlack()
    {
        blackIMG.gameObject.SetActive(false);

    }

    public void rolldis()
    {
        rollbtn.SetActive(false);
    }
    

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= OnTurnChanged;
    }

    void OnTurnChanged(int actor)
    {
        turnIndex++;
        turnText.text = turnIndex.ToString();
        RefreshDiceButton();
      
    }

    public void RefreshDiceButton()
    {
        rollbtn.gameObject.SetActive(TurnManager.Instance.IsMyTurn());
    }

    public void SetPowerUI()
    {
        ChoosePowerUp.gameObject.SetActive(true);   
        switch (playerState.Instance.powerIndex)
        {
            case 0:
                {
                    List<int> powerIndex = new List<int>();
                    powerIndex = randomNodup(0, 4); //DataPool.Instance.powerDatabase1.power1.Count
                    for (int i =0;i<3;i++)
                    {
                        int fixedIndex = powerIndex[i];
                        buttons[i].onClick.RemoveAllListeners();
                        TextMeshProUGUI text = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
                        text.text = DataPool.Instance.powerDatabase1.power1[fixedIndex].title;

                        
                        buttons[i].onClick.AddListener(()=>SetButtonLink(DataPool.Instance.powerDatabase1.power1[fixedIndex].strUP,
                                                                         DataPool.Instance.powerDatabase1.power1[fixedIndex].wisUP,
                                                                         DataPool.Instance.powerDatabase1.power1[fixedIndex].dexUP,
                                                                         DataPool.Instance.powerDatabase1.power1[fixedIndex].vitUP,
                                                                         DataPool.Instance.powerDatabase1.power1[fixedIndex].title,
                                                                          DataPool.Instance.powerDatabase1.power1[fixedIndex].korName));
                        Debug.Log(powerIndex[i]);
                    }
                }
                break;
        }
    }

    public void SetButtonLink(int str, int wis, int dex, int vit, string title,string kor)
    {
        PlayerConfig.str += str;
        PlayerConfig.wis += wis;
        PlayerConfig.dex += dex;
        PlayerConfig.vit += vit;
        PlayerConfig.title = kor + PlayerConfig.title;
        if(PlayerConfig.tribe == string.Empty)
        {
            PlayerConfig.tribe = title;
        }
        MainManager.Instance.InstantiatePlayer();
        PlayerConfig.ApplyToPhoton();
        ChoosePowerUp.gameObject.SetActive(false);

    }

    public List<int> randomNodup(int min,int max)
    {
        List <int> newNum = new List<int>();
        while (newNum.Count < 3)
        {
            int temp = Random.Range(min, max);

            if (!newNum.Contains(temp))
            {
                newNum.Add(temp);
            }
        }
        return newNum;
    }
}
