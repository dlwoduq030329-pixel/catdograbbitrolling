using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class getPlayerInfoUI : MonoBehaviour
{
    BattlePlayer player;
    [SerializeField]
    Slider apSlider;
    [SerializeField]
    TextMeshProUGUI apText;
    [SerializeField]
    TextMeshProUGUI attackInfoText;

    bool isLinked = false;

    public void SetPlayer(BattlePlayer temp)
    {
        player = temp;
        isLinked = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!isLinked) return;
        apSlider.value = player.MyAp /10;
        //float x = 

        apText.text = ((int)player.MyAp).ToString() + "AP";
        if (attackInfoText != null)
        {
            string attackType = player.Ps == playerAttackST.Range ? "원거리 · DEX" : "근거리 · STR";
            attackInfoText.text = $"{attackType}  피해 {player.AttackDamage:0.0}";
        }
    }
}
