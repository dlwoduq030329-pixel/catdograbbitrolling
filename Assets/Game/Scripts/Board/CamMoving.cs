using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class CamMoving : MonoBehaviour
{
    [SerializeField]
    CinemachineVirtualCamera camA;
    [SerializeField]
    CinemachineVirtualCamera camB;
    [SerializeField]
    PlayableDirector timelineGo;
    [SerializeField]
    PlayableDirector timelineBack;

    public void DiceStart()
    {
        timelineGo.Stop();
        timelineGo.time = 0;
        timelineGo.Evaluate();
        timelineGo.Play();
         
    }

    public void DiceEnd()
    {
        timelineBack.Stop();
        timelineBack.time = 0;
        timelineBack.Evaluate();
        timelineBack.Play();
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
