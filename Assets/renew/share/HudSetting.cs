using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HudSetting : MonoBehaviour
{
    [SerializeField]
    Image profileIMG;
    [SerializeField]
    Sprite[] profileSprite;


    [SerializeField]
    CharactorStatus status;
    [SerializeField]
    PlayerWeapon weapon;
    [SerializeField]
    PlayerDeck deck;


    public void OnEnable()
    {
        profileIMG.sprite = profileSprite[status.TribeIndex];
    }


}
