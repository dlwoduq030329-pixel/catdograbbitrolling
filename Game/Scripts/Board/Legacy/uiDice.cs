using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class uiDice : MonoBehaviour
{
    Animator anim;
    Image myIMG;
    [SerializeField]
    Sprite[] rollSp;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        myIMG = GetComponent<Image>();
    }
    public void animStart()
    {
        anim.speed = 1f;
        anim.enabled = true;

        anim.Play("randomRoll");
    }

    public void animSpeed(float x)
    {
        anim.speed = x;
    }

    public void EndRoll(int x)
    {
        anim.Play("Idle");
        anim.enabled = false;
        myIMG.sprite = rollSp[x - 1];
    }



    
}
