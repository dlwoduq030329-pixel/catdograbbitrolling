using UnityEngine;
using Photon.Realtime;
using TMPro;
using System.Runtime.InteropServices;
using UnityEngine.UI;

public class ProfileUI : MonoBehaviour
{
    [SerializeField]
    Sprite[] charactorIMG;
    [SerializeField]
    Image profile;


    private void OnEnable()
    {
        if (!DataConfig.isSelected[0]) return;
        profile.sprite = charactorIMG[DataConfig.tribe];

    }

    public void SetImg()
    {
        profile.sprite = charactorIMG[DataConfig.tribe];

    }



}