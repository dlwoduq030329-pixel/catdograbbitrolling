using ExitGames.Client.Photon;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
public class TagedCard : MonoBehaviour

{

    RectTransform rect;
    Canvas canvas;
    InventorySetting im;
    int cardTempIndex;
    Image cardSP;
    

    [SerializeField]
    RectTransform[] setPos;

    // Start is called before the first frame update
    void Start()
    {
    }

    public void Init(int x)
    {
        cardTempIndex = x;
        cardSP.sprite = DataPool.Instance.cardDatabase.cards[cardTempIndex].myCardSprite;
    }

    // Update is called once per frame
    void Update()
    {

        //this.transform.localPosition = Input.mousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
canvas.transform as RectTransform,Input.mousePosition,
canvas.worldCamera,
out Vector2 pos
);

        rect.localPosition = pos;

        if(Input.GetMouseButtonUp(0))
        {
            CheckPos();
            if(im == null)
            {
                Debug.Log("im NULL");
            }
            im.SetInventoryVerticalScrollEnabled(true);

            this.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        im = GetComponentInParent<InventorySetting>();
        cardSP = GetComponent<Image>();
        im.SetInventoryVerticalScrollEnabled(false);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
     canvas.transform as RectTransform,
     Input.mousePosition,
     canvas.worldCamera,
     out Vector2 pos
 );

        rect.localPosition = pos;
    }

    public void CheckPos()
    {
        Vector2 screenPos =
                 RectTransformUtility.WorldToScreenPoint(null, rect.position);

        foreach (var target in setPos)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(target, screenPos))
            {
                //Set(target);
                nowDeckCard nd = target.GetComponent<nowDeckCard>();
                //nd.Test();
                nd.ChangeSet(cardTempIndex);
                SoundManager.Instance.ChangeCard();
                return;
            }
        }
    }    


}
