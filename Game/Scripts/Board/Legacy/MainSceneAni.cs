using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum aniState
{
    Walk,
    Down
}

public class MainSceneAni : MonoBehaviour
{
    Animator anim;
    string beforeState;
    bool start = false;
    public void Awake()
    {
        anim = GetComponent<Animator>();
        anim.Play("Spawn");
    }
    
    public void ChangejumpIdle()
    {
        anim.Play("Jump_Idle");
    }

    public void ChangeIdle()
    {
        anim.Play("Idle_A");
    }

    public void StartWalk()
    {
        anim.Play("Walking_A");
    }
    // Start is called before the first frame update

  
    public void OnCollisionEnter(Collision collision)
    {
        
        if(collision.gameObject.tag == "Ground"&&!start)
        {
            start = true;
            //Debug.Log("ÂøÁö");
            anim.Play("Jump_Land");
        }
    }
}
