using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GIE
{

    public class Example : MonoBehaviour
    {
        public GetItemEffectType mGetItemEffectType = GetItemEffectType.Explostion_First;
        public string mItemName = "coin";
        public int mItemNumber = 10;
        public Text mItemNumberText;

        //이 코드처럼 실행시키면 됌
        public void OnClickMoney( RectTransform from_where )
        {
            GetItemEffect.mInstance.GetItem(mItemName, mItemNumber, from_where,null, mGetItemEffectType);
        }

    }

}

