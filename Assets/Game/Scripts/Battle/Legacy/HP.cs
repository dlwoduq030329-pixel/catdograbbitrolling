using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class HP : MonoBehaviour
{
    [SerializeField]
    Slider hpSlider;
    [SerializeField]
    bool isPlayer;
    [SerializeField]
    Canvas can;

    float hp;
    float maxhp;
    //battlePlayer bp;
    Slider myHP;
    SetTarGet stg;
    public float Hp => hp;
    RectTransform hprt;

    Vector3 offset = new Vector3(0, 1f, 0);
    public void Awake()
    {
        if (!isPlayer)
        {
            maxhp = 100;
            hp = maxhp;
            Init();
        }

    }


    public void Init()
    {
        can = GameObject.Find("Canvas").GetComponent<Canvas>();

        switch (isPlayer)
        {
            case true:
                {
                    //bp = GetComponent<battlePlayer>();
                   // maxhp = bp.hp;
                    hp = maxhp;

                    SpawnHP();

                }
                break;
            case false:
                {
                   stg = GetComponent<SetTarGet>();
                    SpawnHP();
                }
                break;
        }

    }

    public void DelHP()
    {
        Destroy(myHP);
    }
    
    public void SpawnHP()
    {
        myHP = Instantiate(hpSlider,can.transform);
        hprt = myHP.GetComponent<RectTransform>(); 
        myHP.transform.SetParent(can.transform, false);
        myHP.value = 1;
    }

    public void GetDam(float x)
    {
        hp -= x;
        myHP.value = hp / maxhp;
        
        if(hp<=0)
        {
            if(!isPlayer)
            {          
                stg.isdie = true;
                this.gameObject.SetActive(false);
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (myHP == null||hprt == null) return;

        Vector3 screenPos =
          Camera.main.WorldToScreenPoint(this.transform.position + offset);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
           can.transform as RectTransform,
           screenPos,
           null,
           out Vector2 localPos
       );

        hprt.anchoredPosition = localPos;
    }
}
