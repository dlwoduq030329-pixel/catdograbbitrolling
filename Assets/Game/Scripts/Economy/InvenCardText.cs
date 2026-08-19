using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class InvenCardText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    EquipState st;
    [SerializeField]
    TextMeshProUGUI cardInfo;
    [SerializeField]
    TextMeshProUGUI cardCost;
    [SerializeField]
    GameObject cardInfoOBJ;
    [SerializeField]
    RectTransform cardTrans;
    Canvas canvas;
    bool open = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Evaluation();

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        open = false;
        cardInfoOBJ.gameObject.SetActive(false);
    }

    public void Evaluation()
    {
        EquipData myEquipSt = null;

        switch (st)
        {
            case EquipState.LeftHand:
                {

                    myEquipSt = DataConfig.leftDa;

                    cardTrans.pivot = new Vector2(0, 0);
                }
                break;
            case EquipState.RightHand:
                {
                    myEquipSt = DataConfig.rightDa;
                    cardTrans.pivot = new Vector2(0, 0);
                }
                break;
            case EquipState.Head:
                {
                    myEquipSt = DataConfig.headDa;
                    cardTrans.pivot = new Vector2(1, 0);

                }
                break;
            case EquipState.Body:
                {
                    myEquipSt = DataConfig.bodyDa;
                    cardTrans.pivot = new Vector2(1, 0);

                }
                break;

        }
        if (myEquipSt == null)
        {
            cardInfo.text = null;
            return;
        }
        open = true;
        cardInfoOBJ.SetActive(true);

        cardInfo.text = "STR + " + myEquipSt.stroffset + "\n" +
                              "WIS + " + myEquipSt.wisoffset + "\n" +
                              "DEX + " + myEquipSt.dexoffset + "\n" +
                              "VIT + " + myEquipSt.vitoffset + "\n";
        cardCost.text = "°¡Ä¡ : " + myEquipSt.cost.ToString() + "G" ;
    }
    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();

    }


    // Update is called once per frame
    void Update()
    {
        if (!open) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
        canvas.transform as RectTransform, Input.mousePosition,
        canvas.worldCamera,
        out Vector2 pos
        );

        cardTrans.localPosition = pos;

    }
}
