using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class stateSet : MonoBehaviour
{
    [SerializeField]
    Image playerIMG;
    [SerializeField]
    Sprite[] animal;
    [SerializeField]
    CharactorStatus st;

    public void OnEnable()
    {
        playerIMG.sprite = animal[st.TribeIndex];

    }
}
