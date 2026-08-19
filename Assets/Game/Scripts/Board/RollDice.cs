using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum rollUseage
{
    Move,
    Level,
    Event,
}

public class RollDice : MonoBehaviour,IPointerDownHandler, IPointerUpHandler
{
    rollUseage useage;

    [SerializeField]
    Slider rollSlider;
    [SerializeField]
    LinkSelect linkSelect;
    [SerializeField]
    NodeBasePlayerMov playerMov;
    [SerializeField]
    PlayerToUI playerToUI;
    [SerializeField]
    EventSet es;
    [SerializeField]
    CamMoving cam;
    [SerializeField]
    animSet rollanimRed;
    [SerializeField]
    animSet rollanimBlue;

    [SerializeField]
    GameObject resultDice;
    [SerializeField]
    diceResultUI dice;
    float addGauge = 1f;
    float nowGauge = 0;
    bool isRoll = false;
    private void OnEnable()
    {
        rollSlider.value = 0;
        nowGauge = 0;
        addGauge = 1f;
    }

    public void ChangeUseage(rollUseage roll)
    {
        useage = roll;
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("누름");
        isRoll = true;
        SoundManager.Instance.sliderUp();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("뗌");
        isRoll = false;
        CheckGuage();
        GameManagerInMain.Instance.DeactiveRoll();
        //this.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isRoll) return;

        rollSlider.value += addGauge * Time.deltaTime;
        nowGauge = rollSlider.value;

        if(rollSlider.value >= 1)
        {
            SoundManager.Instance.SliderDown();

            addGauge *= -1;
            rollSlider.value = 1;
        }

        if(rollSlider.value <= 0)
        {
            SoundManager.Instance.sliderUp();

            addGauge *= -1;
            rollSlider.value = 0;
        }
    }

    public void CheckGuage()
    {
        if (nowGauge <= 0.33f)
        {
            int x = Random.Range(1, 5);
            int y = Random.Range(1, 5);
            SetUseAge(x, y);
        }
        else if(nowGauge <=0.66)
        {
            int x = Random.Range(2, 6);
            int y = Random.Range(2, 6);
            SetUseAge(x, y);

        }else
        {
            int x = Random.Range(3, 7);
            int y = Random.Range(3, 7);
            SetUseAge(x, y);
        }
    }


    public void LoadBattleScene()
    {
        SceneManager.LoadScene(2);

    }

    public void SetUseAge(int x,int y)
    {
        switch(useage)
        {
            case rollUseage.Move:
                {


                    linkSelect.LinkDice(x,y,4);

                    cam.DiceStart();

                    GameManagerInMain.Instance.AddTurn();
                    rollanimRed.animRoll(x);
                    rollanimBlue.animRoll(y);
                    DataConfig.turn++;
                    GameManagerInMain.Instance.ShowTurnUI();
                }
                break;
            case rollUseage.Level:
                {


                    DataConfig.hard = x;
                    DataConfig.count =Random.Range(1,7);
                    Debug.Log("전투로 이동합니다 \n 적 난이도 : " + DataConfig.hard + "\n적 갯수 : " + DataConfig.count);
                    dice.Init(x);
                    resultDice.SetActive(true);
                    //전투 씬으로 이동
                    //dataconfig에서 위치 index를 전송해줘야함.
                    //ChangeUseage(rollUseage.Move);

                    Invoke(nameof(LoadBattleScene), 1.5f);
                }
                break;
            case rollUseage.Event:
                {
                    //playerToUI.GetState();
                    //
                    //es.SuccessCheck(playerToUI.EventIndex, x);
                }
                break;
        }
    }
}
