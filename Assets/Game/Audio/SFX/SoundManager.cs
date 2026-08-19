using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [SerializeField]
    AudioSource[] audios;

    private static SoundManager instance;
    public static SoundManager Instance => instance;
    private void Awake()
    {
        if(instance == null) 
            instance = this;
    }

    public void Walk()
    {
        audios[0].Play();
    }

    public void Dash()
    {
        audios[1].Play();
    }

    public void GetWeapon()
    {
        audios[2].Play();
    }

    public void EventText()
    {
        audios[3].Play();
    }

    public void BoxDown()
    {
        audios[4].Play();
    }
    public void BoxOpen()
    {
        audios[5].Play();
    }

    public void lockSuccess()
    {
        audios[6].Play();
    }

    public void lockFail()
    {
        audios[7].Play();
    }

    public void sliderUp()
    {
        audios[8].Play();
    }

    public void SliderDown()
    {
        audios[9].Play();
    }

    public void RollDice()
    {
        audios[10].Play();
    }

    public void DiceSet()
    {
        audios[11].Play();
    }

    public void OpenInven()
    { audios[12].Play();}

    public void ChangeCard()
        { audios[13].Play();}
    public void OpenEvent()
        { audios[14].Play();}

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
