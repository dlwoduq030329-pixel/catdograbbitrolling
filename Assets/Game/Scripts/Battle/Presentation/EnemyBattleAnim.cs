using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyBattleAnim : MonoBehaviour
{
    [SerializeField]
    bool duelAnim = false;
    [SerializeField]
    float dieTime;

    Animator enemyAnim;

    private void Awake()
    {
         enemyAnim = GetComponent<Animator>();
    }

    public void Idle()
    {
        enemyAnim.Play("Idle");
    }

    public void Attack()
    {
        int attackPool = duelAnim ? 2 : 1;
        int attack = Random.Range(0, attackPool);

        switch(attack)
        {
            case 0:
                {
                    enemyAnim.Play("Attack");
                }
                break;
            case 1:
                {
                    enemyAnim.Play("Attack1");
                }
                break;
        }
    }

    public void Walk()
    {
        enemyAnim.Play("Walk");
    }

    public void Die()
    {
        enemyAnim.Play("Die");
        //Invoke("DisSet", dieTime);
    }

    public void DisSet()
    {
        this.gameObject.SetActive(false);

    }

    private void OnDisable()
    {
        //Debug.Log("»Í!");
    }


}
