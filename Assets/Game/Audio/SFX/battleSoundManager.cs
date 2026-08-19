using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class battleSoundManager : MonoBehaviour
{
    [SerializeField]
    AudioSource[] bs;

    private static battleSoundManager instance = null;
    public static battleSoundManager Instance => instance;
    private void Awake()
    {
        if(instance== null)
            instance = this;
    }

    public void Slash()
    {
        bs[0].Play();
    }
    public void Arrow()
        { bs[1].Play(); }
    public void Heal()
        { bs[2].Play(); }
    public void getDam()
        { bs[3].Play(); }
    public void fireMagic()
        { bs[4].Play(); }
    public void lightMagic()
        { bs[5].Play(); }
    public void iceMagic()
        { bs[6].Play(); }
    public void HardAttack()
        { bs[7].Play(); }
    public void WheelAttack()
        { bs[8].Play(); }
    public void GroundMagic()
        { bs[9].Play(); }
    public void GameOver()
        { bs[10].Play(); }

    public void MagicAttack()
    {
        bs[11].Play();
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
