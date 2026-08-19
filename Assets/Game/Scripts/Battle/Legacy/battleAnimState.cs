using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum battleAnim
{
    attack,
    walk
}

public class battleAnimState : MonoBehaviour
{
    Animator anim;
    battleAnim ba;
    private void Awake()
    {
        anim = GetComponent<Animator>();
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Attack()
    {
        anim.SetTrigger("attack");
    }

    public void SetWalk()
    {
        anim.SetBool("walk", true);
    }

    public void SetIdle()
    {
        anim.SetBool("walk", false);
    }

    public void attackSetIdle()
    {
        anim.SetTrigger("goidle");
    }
}
