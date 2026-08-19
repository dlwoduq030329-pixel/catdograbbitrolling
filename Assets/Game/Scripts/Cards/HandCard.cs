using Coffee.UIEffects;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; 

public class HandCard : MonoBehaviour
{
    [SerializeField]
    Vector3[] cardPos;
    [SerializeField]
    GameObject cardOrigin;
    [SerializeField]
    Canvas Canvas;
    [SerializeField]
    GameObject playGameUI;
    GameObject[] handCard;
    WaitForSeconds drawCool = new WaitForSeconds(0.3f);
    WaitForSeconds checkCool = new WaitForSeconds(0.2f);

    bool test = false;
    bool firstDraw = false;
    int activeIndex;
    public void Start()
    {
        handCard = new GameObject[5];
   
    }

    public void CardInit()
    {
        Debug.Log("고");
        StartCoroutine(FirstCardDraw());
    }

    IEnumerator Carddraw(int x)
    {
        yield return checkCool;
        handCard[x].gameObject.SetActive(true);
    }

    public IEnumerator FirstCardDraw()
    {
        for(int i =0;i<5;i++)
        {
            handCard[i] = Instantiate(cardOrigin, this.transform);
            handCard[i].transform.localPosition = cardPos[i];
            //처음 5장을 드로우 그 후 덱에서
            //내 덱에는 index만 존재. 즉 몇번째 카드를 드로우 할지를 temp를 통해 결정.
            //결정된 카드의 Listindex를 통해 deck에서 hand로 옮김.
            int temp = Random.Range(0, battleSystem.Instance.deck.Count);
            cardOwn cardown = handCard[i].GetComponent<cardOwn>();
            //Debug.Log(battleSystem.Instance.deck[temp]);//0123401234
            Debug.Log("CardIndex : " + battleSystem.Instance.deck[temp]);
            cardown.CardInit(battleSystem.Instance.deck[temp]);
            battleSystem.Instance.DrawCard(battleSystem.Instance.deck[temp]);
          
            yield return drawCool;
        }
        playGameUI.SetActive(true);

        DOTweenAnimation dOTweenAnimation = playGameUI.GetComponent<DOTweenAnimation>();
        dOTweenAnimation.DORestartById("10");
        yield return new WaitForSeconds(1.5f);
        playGameUI.SetActive(false);

        battleSystem.Instance.MovStart();
        firstDraw = true;
        //battleSystem.Instance.GamePlay();
    }
   
    

    private void Update()
    {

    


        if (!test)
        {
            if (battleSystem.Instance != null)
            {
                battleSystem.Instance.cueSign += CardInit;
                test = true;

                battleSystem.Instance.GamePlay();
            }
        }
        if (!firstDraw) return;
        for (int i = 0; i < 5; i++)
        {
            if (!handCard[i].activeSelf)
            {
                StartCoroutine(Carddraw(i));
            }
        }


    }

    public void goActive()
    {

    }


    public void OnDisable()
    {
        battleSystem.Instance.cueSign -= CardInit;
    }



}
