using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStateMachine : MonoBehaviour
{
    [SerializeField]
    playerSt playerSt;
    [SerializeField]

    playerSt beforeSt;
    [SerializeField]
    GameObject target;
    public GameObject Target => target;
    BattlePlayer player;
    UnitState unit;
    public playerSt BeforeST => beforeSt;
    [SerializeField]
    float moveSpeed;
    [SerializeField]
    GameObject rangeOBJ;
    public float MoveSpeed => moveSpeed;
    bool attackState = false;
    bool sc = false;

    bool isWaiting = false;
    public bool IsWaiting=> isWaiting;

    EnemyBattleAnim eba;

    UnitState us;
    private void Awake()
    {
        player = GetComponent<BattlePlayer>();
        eba = GetComponent<EnemyBattleAnim>();
        unit = GetComponent<UnitState>();
    }

    public IEnumerator waitingChange(playerSt temp)
    {
        EnemySkill skill = GetComponent<EnemySkill>();
        yield return new WaitUntil(()=>!skill.SkillUsing);
        ChangePlayerState(temp);
        isWaiting = false;
    }

    public void ChangePlayerState(playerSt st)
    {
        if (playerSt == st)
        {
            Debug.Log("리턴!");
            return;
        }

   /*     if(playerSt == playerSt.Skill)
        {
            isWaiting = true;
            StartCoroutine(waitingChange(st));
            return;
        }*/
        
        attackState = false;


        if (playerSt != playerSt.none)
        {
            StopCoroutine(playerSt.ToString());
        }
        beforeSt = playerSt;
        playerSt = st;


        Debug.Log(this.gameObject.name + st.ToString() + "으로 변경");
        StartCoroutine(playerSt.ToString());

        if(!sc)
        {
            sc = true;
            StartCoroutine(skillCool());
        }
    }
  

    public bool returnState()
    {
        bool temp = false;
        if (unit.IsTaunt ||
            unit.IsStun ||
            unit.IsKnockback)
        {
            temp = true;
        }

        return temp;
    }

    public IEnumerator Idle()
    {
        eba.Idle();
        target = null;

        yield return null;
        ChangePlayerState(playerSt.Detect);
    }

    public IEnumerator Detect()
    {
        if (target == null)
        {
            //target = BattleManager.Instance.Player;
            target = BattleManager.Instance.returnClosePlayer(this.gameObject);
            if (target == null)
            {
                ChangePlayerState(playerSt.Skill);
            }
        }


        yield return null;
        ChangePlayerState(playerSt.Move);
    }

    public void lookTarget()
    {
        if (target == null)
        {
            return;
        }
        Vector3 targetPos = target.transform.position;
        targetPos.y = this.transform.position.y;

        this.transform.LookAt(targetPos);
    }

    public IEnumerator Move()
    {
        eba.Walk();
        float tempDis;
        do
        {
            lookTarget();
            if (returnState())
            {
                yield return null;
                tempDis = Vector3.Distance(this.transform.position, target.transform.position);
                continue;
            }
            if(target == null)
            {
                ChangePlayerState(playerSt.Detect);
                yield break;
            }
            tempDis = Vector3.Distance(this.transform.position, target.transform.position);

            Vector3 tempVec = target.transform.position;
            tempVec.y = this.transform.position.y;

            this.transform.position = Vector3.MoveTowards(this.transform.position, tempVec, moveSpeed * Time.deltaTime);
            yield return null;
        } while (tempDis >= player.Range);

        ChangePlayerState(playerSt.Attack);
    }

    public void TargetChange(GameObject temp)
    {
        GameObject beforeTarget = target;
        GameObject testTarget = temp;

        if(testTarget == null)
        {
            target = beforeTarget;
        }else
        {
            target = testTarget;
        }
        ChangePlayerState(playerSt.Move);
        //StopAllCoroutines();
        Debug.Log(this.gameObject.name + "타겟 변경 Move상태 변경");
        //ChangePlayerState(playerSt.Move);
    }

    public IEnumerator Attack()
    {
        if (attackState)
        {
            Debug.Log("중복 진입 불가!");
            yield break;
        }
        attackState = true;

        Debug.Log("진입 성공");



        if (target == null)
        {
            Debug.Log("타겟 널");

            ChangePlayerState(playerSt.Detect);
            yield break;
        }
        float targetHP = target.GetComponent<BattlePlayer>().HP;

        while (targetHP >0)
        {
            Debug.Log("공격 코루틴 반복중!");
            targetHP = target.GetComponent<BattlePlayer>().HP;
            us = target.GetComponent<UnitState>();

            lookTarget();
            eba.Attack();
            if (returnState())
            {
                yield return null;
                continue;
            }
            
            if(player.Range >= 9f)
            {
                var temp = Instantiate(rangeOBJ, this.transform.position,Quaternion.identity);
                temp.GetComponent<rangeAttack>().Init( player.AttackDamage, target);
                battleSoundManager.Instance.MagicAttack();

            }
            else
            {
                
               

            }
            targetHP = target.GetComponent<BattlePlayer>().HP;
            float attackRate = player.AttackSpeed;
            if(unit.IsCold)
            {
                attackRate *= 0.5f;
            }
            yield return new WaitForSeconds(attackRate);
            eba.Idle();

            if(Vector3.Distance(this.gameObject.transform.position,target.transform.position) > player.Range)
            {
                ChangePlayerState(playerSt.Move);
                yield break;
            }

        }

        //BattleManager.Instance.RemovePlayer(target);
        //ChangePlayerState(playerSt.Idle);
    }

    public void meleeAttack()
    {
        us = target.GetComponent<UnitState>();
        us.GetDam(player.AttackDamage);
        us.idleHitDam();
        battleSoundManager.Instance.Slash();
    }

    public IEnumerator skillCool()
    {
        float skilltime =0;
        while (true)
        {
            yield return null;
            if (playerSt == playerSt.Skill) continue;
            skilltime += Time.deltaTime;

            if(skilltime >=6)
            {
               //Debug.Log("스킬 사용!");
                skilltime = 0;

                EnemySkill es;

                if (TryGetComponent<EnemySkill>(out es))
                {

                    ChangePlayerState(playerSt.Skill);
                    //GetComponent<EnemySkill>().UseSkill();
                    es.UseSkill();
                    //yield return new WaitForSeconds(3f);
                    yield return null;
                }
            }
        }
    }

    public IEnumerator Skill()
    {
        while (true)
        {
            yield return null;
        }
    }

    public IEnumerator Die()
    {
        BattleManager.Instance.RemoveEne(this.gameObject);
        yield return null;
        this.gameObject.SetActive(false);
    }

    public float ReturnDistance()
    {
        return Vector3.Distance(this.transform.position, target.transform.position);
    }
}
