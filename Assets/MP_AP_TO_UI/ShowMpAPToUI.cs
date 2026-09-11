using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class ShowMpAPToUI : MonoBehaviour
{
    [SerializeField]
    Image[] mpImages;
    [SerializeField]
    Sprite nullMp;
    [SerializeField]
    Sprite fullMP;
    [SerializeField]
    TextMeshProUGUI mpText;

    [SerializeField]
    Image[] apImages;
    [SerializeField]
    Sprite nullAp;
    [SerializeField]
    Sprite fullAP;
    [SerializeField]
    TextMeshProUGUI apText;

    int maxMp;
    int nowMp;

    int maxAp;
    int nowAp;

    public void StartMPInit(int maxmp) //매 턴 시작할때 호출.
    {
        maxMp = maxmp;
        nowMp = maxMp;
        SetMpUI();
    }

    public void StartAPInit(int maxap) //매 턴 시작할때 호출.
    {
        maxAp = maxap;
        nowAp = maxAp;
        SetApUI();
    }


    public void SetMpUI()
    {
        int mpCount = maxMp;
        mpText.text = nowMp + "/" + maxMp;
        if(mpCount>9)
        {
            mpCount = 9;
        }

        for(int i =0;i<mpImages.Length;i++)
        {
            if(i<mpCount)  
                mpImages[i].sprite = fullMP;
            else
                mpImages[i].sprite = nullMp;
        }
    }

    public void SetApUI()
    {
        int apCount = maxAp;
        apText.text = nowAp + "/" + maxAp;
        if (apCount > 9)
        {
            apCount = 9;
        }

        for (int i = 0; i < apImages.Length; i++)
        {
            if (i < apCount)
                apImages[i].sprite = fullAP;
            else
                apImages[i].sprite = nullAp;
        }
    }


    public void useMp(int cost)
    {
        //방어 코드는 호출부에서
        nowMp -= cost;

        for(int i = nowMp + cost - 1;i< nowMp - 1; i--) //매직넘버 일단 넘어가
        {
            mpImages[i].sprite = nullMp;
        }
       
        SetMpUI();

    }

    public void useAp(int cost)
    {
        //방어 코드는 호출부에서
        nowAp -= cost;

        for (int i = nowAp + cost - 1; i < nowAp - 1; i--) //매직넘버 일단 넘어가
        {
            apImages[i].sprite = nullAp;
        }

        SetApUI();

    }

}
