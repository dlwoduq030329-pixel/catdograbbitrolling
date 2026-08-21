using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatusUI : MonoBehaviour
{
    [Tooltip("캐릭터 선택 후 스텟 부여 UI 전반 담당.")]
    [Header("코드 설명용 변수. 마우스를 올려 확인.")]
    [SerializeField]
    bool CODE_EXPLAIN;

    [Tooltip("캐릭터별 설명.")]
    [SerializeField]
    string[] charactorText;
    [Tooltip("캐릭터별 기본 칭호")]
    [SerializeField]
    string[] charactorTitleText;
    [Tooltip("캐릭터별 이름")]
    [SerializeField]
    string[] charactornameText;
    [SerializeField]
    TextMeshProUGUI charactorStory;
    [SerializeField]
    TextMeshProUGUI charactorName;
    [SerializeField]
    TextMeshProUGUI charactorTitle;

    [SerializeField]
    Slider[] statusSliders;

    [SerializeField]
    int leastpoint;
    [SerializeField]
    TextMeshProUGUI leastPointText;
    [SerializeField]
    TextMeshProUGUI[] statusTexts;

    int PowerPoint1;
    int PowerPoint2;
    int least;
    public int playerIndex = -1;

    [SerializeField]
    Image charactorIMG;
    [SerializeField]
    Sprite[] CharactorSp;

    [SerializeField]
    SpawnPlayer summon;

    int[] temporaryStatus = new int[6];
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TextSet(int charactorIndex)
    {
        leastpoint = 10;
        leastPointText.text = "잔여 포인트 : " + leastpoint; 
        charactorStory.text = charactorText[charactorIndex];
        charactorName.text = charactornameText[charactorIndex];
        charactorTitle.text = charactorTitleText[charactorIndex];
        charactorIMG.sprite = CharactorSp[charactorIndex];
        playerIndex = charactorIndex;
        switch(charactorIndex)
        {
            case 0:
                InitST(1, 4, 2);
                break;
           case 1:
                InitST(0, 5, 2);
                break;
            case 2:
                InitST(2,3, 2);
                break;
        }
    }

    public void InitST(int status1,int status2, int Up)
    {
        PowerPoint1 = status1;
        PowerPoint2 = status2;
        least = Up;
        for(int i =0;i<statusSliders.Length;i++)
        {
            if(i == status1|| i == status2)
            {
                temporaryStatus[i] = Up;
               // setPoint();
            }
            else
            {
                temporaryStatus[i] = 0;
                //setPoint();
            }
            setPoint();
        }
    }

    public void UpPoint(int statusIndex) //+버튼에 할당 인자는 스텟별 index
    {
        if (leastpoint <= 0) return;
        leastpoint--;
        leastPointText.text = "잔여 포인트 : " + leastpoint;
        temporaryStatus[statusIndex]++;
        setPoint();
    }

    public void DownPoint(int statusIndex)//-버튼에 할당 인자는 스텟별 index
    {
        if (temporaryStatus[statusIndex] <= 0) return;

        if(statusIndex==PowerPoint1||statusIndex==PowerPoint2)
        {
            if (temporaryStatus[statusIndex] <= least) return;

            leastpoint++;
            temporaryStatus[statusIndex]--;
            leastPointText.text = "잔여 포인트 : " + leastpoint;
            setPoint();
        }else
        {
            leastpoint++;
            temporaryStatus[statusIndex]--;
            leastPointText.text = "잔여 포인트 : " + leastpoint;
            setPoint();

        }

    }

    public void setPoint() //스텟 slider 적용
    {
        for(int i =0;i<temporaryStatus.Length;i++)
        {
            statusSliders[i].value = (float)temporaryStatus[i] / 12;
            statusTexts[i].text = temporaryStatus[i].ToString();
        }
    }

    public void summonPlayer() //플레이어 소환 및 플레이어 기본 정보
    {
        if (playerIndex == -1) return;
        summon.PlayerInfoInit(temporaryStatus);
        summon.PlayerPosInit(playerIndex);
    }
}
