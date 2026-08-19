using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum RewardSt
{
    equipment,
    card,
    status
}



public class RewardPopUp : MonoBehaviour
{
    [SerializeField]
    Button[] rewardBtn;

    public void OnEnable()
    {
        randomReward();
    }

    public void randomReward()
    {
        for(int i =0;i<3;i++)
        {
            RewardSt state = (RewardSt)Random.Range(1, 3);
            rewardBtn[i].gameObject.GetComponent<cardTag>().Init(state);
        }
  
    }

    public void SetReward()
    {
    }


    
}
