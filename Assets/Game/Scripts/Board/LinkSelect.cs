using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LinkSelect : MonoBehaviour
{
    [SerializeField]
    Button[] selectBTN;
    [SerializeField]
    GameObject selectPanel;
    [SerializeField]
    NodeBasePlayerMov playerMov;
    [SerializeField]
    Sprite[] diceSP;
    [SerializeField]
    TextMeshProUGUI rollUseageText;
    [SerializeField]
    Image[] ChooseIMG;
    [SerializeField]
    RectTransform rect;
    [SerializeField]
    Sprite[] redSp;
    [SerializeField]
    Sprite[] blueSp;
    List<int> chooseList = new List<int>();

    [SerializeField]
    int tribeIndex;
    
    public void selectUI(bool temp)
    {
        selectPanel.SetActive(temp);
        Debug.Log("3번 통과");
    }

    public void LinkTribe()
    {
        LinkDel();

        chooseList.Clear();   
        for(int i =0;i<3;i++) 
        {
            chooseList.Add(i);
        }
        Shffle();
        for(int i =0;i<3;i++)
        {
            int temp = chooseList[i];
            selectBTN[i].image.sprite = DataPool.Instance.powerDatabase1.power1[temp].myCardSprite;
            selectBTN[i].onClick.AddListener(() => SetTribe(DataPool.Instance.powerDatabase1.power1[temp].index));
            selectBTN[i].gameObject.SetActive(true);
        }
        selectUI(true);
    }

    public void LinkWork()
    {
        LinkDel();
        rect.anchoredPosition =
        new Vector2(-537f, rect.anchoredPosition.y);
        rollUseageText.text = "직업 선택";
        chooseList.Clear();

        int count = Mathf.Min(3, DataPool.Instance.powerDatabase3.power3.Count);
        for (int i = 0; i < DataPool.Instance.powerDatabase3.power3.Count; i++) chooseList.Add(i);
        Shffle();
        //for (int i = 0; i < selectBTN.Length; i++) selectBTN[i].gameObject.SetActive(i < count);
        for (int i = 0; i < 3; i++)
        {
            int temp = chooseList[i];
            selectBTN[i].image.sprite = DataPool.Instance.powerDatabase2.power2[temp].myCardSprite;
            selectBTN[i].onClick.AddListener(() => SetWork(DataPool.Instance.powerDatabase2.power2[temp].index));
            selectBTN[i].gameObject.SetActive(true);
        }
        selectUI(true);

    }
    public void LinkTitle()
    {
        Debug.Log("2번 통과");
        LinkDel();
        rollUseageText.text = "칭호 선택";
        chooseList.Clear();
        int count = Mathf.Min(3, DataPool.Instance.powerDatabase2.power2.Count);
        for (int i = 0; i < DataPool.Instance.powerDatabase3.power3.Count; i++) chooseList.Add(i);
        Shffle();
        //for (int i = 0; i < selectBTN.Length; i++) selectBTN[i].gameObject.SetActive(i < count);
        for (int i = 0; i < 3; i++)
        {
            int temp = chooseList[i];
            selectBTN[i].image.sprite = DataPool.Instance.powerDatabase3.power3[temp].myCardSprite;
            selectBTN[i].onClick.AddListener(() => SetTitle(DataPool.Instance.powerDatabase2.power2[temp].index));
            selectBTN[i].gameObject.SetActive(true);
            selectBTN[i].GetComponentInChildren<TextMeshProUGUI>().text = DataPool.Instance.powerDatabase3.power3[temp].title;
        }
        selectUI(true);
    }

    public void LinkDel()
    {
        for(int i =0;i<3;i++)
        {
            selectBTN[i].onClick.RemoveAllListeners();
        }
    }

    public void LinkDice(int firstDice,int secondDice)
    {
        LinkDel();
        selectBTN[0].gameObject.SetActive(false);
        selectBTN[1].onClick.AddListener(() => playerMov.MovCor(firstDice));
        selectBTN[2].onClick.AddListener(() => playerMov.MovCor(secondDice));
        selectPanel.SetActive(true);
    }

    public void LinkDice(int firstDice, int secondDice,float Time)
    {
        rollUseageText.text = "주사위 선택";
        LinkDel();
        rect.anchoredPosition =
    new Vector2(-322f, rect.anchoredPosition.y);
        selectBTN[1].gameObject.SetActive(false);

        selectBTN[0].image.sprite = redSp[firstDice-1];
        selectBTN[2].image.sprite = blueSp[secondDice-1];

        selectBTN[0].onClick.AddListener(() => playerMov.MovCor(firstDice));
        selectBTN[2].onClick.AddListener(() => playerMov.MovCor(secondDice));
        Invoke(nameof(pannelActive), Time);
    }

    public void pannelActive()
    {
        SoundManager.Instance.DiceSet();

        selectPanel.SetActive(true);
    }

    public void Shffle()
    {
        for (int i = 0; i < chooseList.Count; i++)
        {
            int rand = Random.Range(i, chooseList.Count);
            (chooseList[i], chooseList[rand]) = (chooseList[rand], chooseList[i]);
        }
    }

    public void SetTribe(int x)
    {
        tribeIndex = x;
        GameManagerInMain.Instance.setTribe();
        GameManagerInMain.Instance.setTitle();
        selectUI(false);
    }

    public int returntribe()
    {
        return tribeIndex;
    }

    public void SetTitle(int index)
    {
        Power3Data data = DataPool.Instance.powerDatabase3.power3.Find(item => item.index == index);
        if (data == null) return;
        DataConfig.playerDatas[0] += data.strUp;
        DataConfig.playerDatas[1] += data.wisUP;
        DataConfig.playerDatas[2] += data.dexUP;
        DataConfig.playerDatas[3] += data.vitUP;
        //if (data.addCardIndex >= 0) DataConfig.AddDic(data.addCardIndex, 1);
        DataConfig.selectedTitleIndex = index;
        DataConfig.stage = 3;

        GameManagerInMain.Instance.AddTitle(data.title);

        if (data.activeFuncName != "NULL")
        {

            Invoke(data.activeFuncName,0f);
        }


        selectUI(false);

        //FinishProgressSelection();
    }

    public void SetFlame()
    {
        DataConfig.AddDic(20, 2);
        DataConfig.AddDic(23, 2);
        DataConfig.AddDic(24, 2);
        DataConfig.AddDic(27, 2);
        //DataConfig.AddDic(8, 2);

    }

    public void SetWork(int index)
    {
        Power3Data data = DataPool.Instance.powerDatabase3.power3.Find(item => item.index == index);
        if (data == null) return;
        DataConfig.playerDatas[0] += data.strUp;
        DataConfig.playerDatas[1] += data.wisUP;
        DataConfig.playerDatas[2] += data.dexUP;
        DataConfig.playerDatas[3] += data.vitUP;
        DataConfig.selectedJobIndex = index;
        DataConfig.stage = 4;
        FinishProgressSelection();
    }

    void FinishProgressSelection()
    {
        selectUI(false);
        playerMov.GetComponent<CheckEvent>().CompleteStartNodeSelection();
        DataConfig.SaveData();
        GameManagerInMain.Instance.activeRoll(rollUseage.Move);
    }

}
