using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardUse : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{

    Vector3 originPos;
    Animator anim;
    RectTransform rect;
    Canvas canvas;
    cardOwn co;
    bool isEnter = false;

    GameObject player;
    PlayerStateMachine pfsm;
    BattlePlayer bPlayer;
    BattleCard card;

    [SerializeField]
    GameObject[] disolveCard;
    MultiColorDissolve md;
    bool isInit = false;


    private Vector3 offset;


    public void OnPointerDown(PointerEventData eventData)
    {

        offset = transform.position - Input.mousePosition;
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        //-410 => -190
        cardOwn own = GetComponent<cardOwn>();

        if (originPos.y - this.transform.localPosition.y <= -220f )
        {
            if(own.useAP <= bPlayer.MyAp)
            {

                if (pfsm.PlayerSt == playerSt.Skill)
                {
                    GoOrigin();
                }else
                {
                    bPlayer.UseAp(own.useAP);
                    pfsm.ChangePlayerState(co.cardName);
                    card.UseCard(own.cardIndex);
                    StartCoroutine(CardUseAnim());

                }

            }
            else
            {
                GoOrigin();
            }
        }
        else
        {
            GoOrigin();
        }

    }

    IEnumerator HoverAnim()
    {
        isEnter = true;
        if(anim.GetCurrentAnimatorStateInfo(0).IsName("DrawCard"))
        {
            //SavePos();
        }
        if (isInit)
        {
            //anim.Play("Hover");

        }


        yield return new WaitForSeconds(0.3f);

        isEnter = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {

        if (isEnter) return;
        StartCoroutine(HoverAnim());

        if(pfsm == null)
        {
            Debug.Log(player == null);
            pfsm = player.GetComponent<PlayerStateMachine>();

        }
        card.OpenInfo(co.cardIndex);
       

       anim.SetBool("myState", true);
        

        // Debug.Log("마우스 들어옴!");
    }

    public void GoOrigin()
    {
        Debug.Log("Before : " + originPos);

        this.transform.localPosition = originPos;
        Debug.Log("After : " + transform.localPosition);

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //if (!isEnter) return;
        card.CloseInfo();
        //isEnter = false;
        anim.SetBool("myState", false);
        //Debug.Log("오리진 : " + originPos);

    }


    public void SavePos()
    {
        originPos = this.transform.localPosition;
        isInit = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(pfsm.PlayerSt == playerSt.Die) return;
        if (!isInit) return;
        transform.position = Input.mousePosition + offset;
    }

   public void Init()
    {
        player = BattleManager.Instance.Player;
        bPlayer = player.GetComponent<BattlePlayer>();
        co = GetComponent<cardOwn>();
        card = GetComponentInParent<BattleCard>();

    }

    // Start is called before the first frame update
    void Start()
    {
        player = BattleManager.Instance.Player;
        bPlayer = player.GetComponent<BattlePlayer>();
        co = GetComponent<cardOwn>();
        card = GetComponentInParent<BattleCard>();
        md = GetComponent<MultiColorDissolve>();
    }

    public void Awake()
    {
        anim = GetComponent<Animator>();
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    //public void 

    private void OnEnable()
    {
        anim.SetTrigger("draw");
        foreach(var temp in disolveCard)
        {
            temp.gameObject.SetActive(false);
        }

    }

    public IEnumerator CardUseAnim()
    {
        foreach (var temp in disolveCard)
        {
            temp.gameObject.SetActive(true);
        }
        md.Play();

        yield return new WaitForSeconds(1f);

        //디졸브 방법 까먹음
        isInit = false;
        this.gameObject.SetActive(false);
    }

    

    // Update is called once per frame
    void Update()
    {
        
    }
}
