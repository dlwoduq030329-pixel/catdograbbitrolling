using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class weaponIMG : MonoBehaviour
{
    [Header("0-left,1-right,2-head,3-body")]
    [SerializeField]
    int index;
    Image img;

    public void OnEnable()
    {
        if(img == null)
        {
            img = GetComponent<Image>();
        }
        switch(index)
        {
            case 0:
                {
                    img.sprite = DataPool.Instance.equipDatabase.equip[DataConfig.leftHand].myEquipSprite;
                    break;
                }
            case 1:
                {
                    img.sprite = DataPool.Instance.equipDatabase.equip[DataConfig.rightHand].myEquipSprite;

                    break;
                }
            case 2:
                {
                    img.sprite = DataPool.Instance.equipDatabase.equip[DataConfig.head].myEquipSprite;

                    break;
                }
            case 3:
                {
                    img.sprite = DataPool.Instance.equipDatabase.equip[DataConfig.body].myEquipSprite;

                    break;
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
        
    }
}
