using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class rollDice : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{

    public static rollDice Instance;
    [SerializeField]
    Slider powerBar;

    float poweroffset = 0.005f;
    bool isRoll = false;

    public delegate void MovStart(int x);
    public MovStart mov;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    // Start is called before the first frame update
    void Start()
    {
        powerBar.value = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if(!isRoll&&Input.GetKey(KeyCode.Space))
        {
            isRoll = true;
            StartCoroutine(roll());
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            isRoll = false;
            RollDice(powerBar.value);
        }

    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if(!isRoll)
        {
            Debug.Log("눌렀다");
            isRoll = true;
            StartCoroutine(roll());
        }
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isRoll)
        {    
            isRoll =false;
            RollDice(powerBar.value);
        }

    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isRoll = false;
        RollDice(powerBar.value);

    }

    public void RollDice(float x)
    {
        int temp = 0;
        if(x<0.33f)
        {
            temp = Random.Range(1, 4);
        }else if(x>=0.33f && x<0.66f)
        {
            temp = Random.Range(3, 5);
        }else if(x>0.66f)
        {
            temp = Random.Range(4, 7);
        }
        Debug.Log(temp + "주사위는 굴려졌다!");
        mov?.Invoke(temp);
        //this.gameObject.SetActive(false);
        //UIManager.Instance.RefreshDiceButton();
        UIManager.Instance.rolldis();
    }

    public void RollDiceForBattle()
    {
        if(PhotonNetwork.IsMasterClient)
        {
            int enemyCount = Random.Range(0, 7);
            int enemyIndex = Random.Range(0, 7);
            playerState.Instance.enemyCount = enemyCount;
            playerState.Instance.enemyIndex = enemyIndex;
            //주사위 굴리는 애니메이션 rpc로 실행.
        }
    }

    public IEnumerator roll()
    {
        while(isRoll)
        {
            powerBar.value += poweroffset;
            if(powerBar.value == 1 || powerBar.value == 0)
            {
                poweroffset *= -1;
            }
            yield return null;
        }
    }
}
