using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum playerSt
{
    none,
    Idle,
    Move,
    Detect,
    Attack,
    Skill,
    Die
}

public enum playerAttackST
{
    Hand,
    Melee,
    Range
}

public class PlayerStateMachine : MonoBehaviour
{
    [SerializeField]
    playerSt playerSt;
    [SerializeField]
    playerSt beforeSt;
    GameObject target;
    public GameObject Target => target;
    BattlePlayer player;
    PlayerSkills skill;
    UnitState unit;
    public playerSt BeforeST => beforeSt;
    public playerSt PlayerSt => playerSt;
    
    [SerializeField]
    float moveSpeed;
    public float MoveSpeed => moveSpeed;
    [SerializeField]
    GameObject rangeOBJ;
    bool attackState = false;
    float offset = 1;
    public float Offset
    {
        get { return offset; }
        set { offset = value; }
    }

    float damoffset;
    public float DamOffset
    {
        get { return damoffset; }
        set { damoffset = value; }
    }
    Animator playerAnim;

    float dam;
    UnitState us;

    private void Awake()
    {
        player = GetComponent<BattlePlayer>();
        skill = GetComponent<PlayerSkills>();
        unit = GetComponent<UnitState>();
        playerAnim = GetComponent<Animator>();
    }

    public void ChangePlayerState(playerSt st)
    {
        playerAnim.speed = 1;
        if (playerSt == st)
        {
           // Debug.Log("리턴!");
            return;
        }
        attackState = false;


        if (playerSt != playerSt.none)
        {
            StopCoroutine(playerSt.ToString());
        }
        beforeSt = playerSt;
        playerSt = st;

       
       // Debug.Log(st.ToString() + "으로 변경");
        StartCoroutine(playerSt.ToString());
    }
    public void ChangePlayerState(string skillname)
    {
        if(playerSt != playerSt.Skill)
        {
            beforeSt = playerSt;
        }
        StopCoroutine(playerSt.ToString());
        playerSt = playerSt.Skill;
        skill.UseSkill(skillname);
    }

    public bool returnState()
    {
        bool temp = false;
        if(unit.IsTaunt ||
            unit.IsStun||
            unit.IsKnockback)
        {
            temp = true;
        }

        return temp;
    }
    public IEnumerator Skill()
    {
        while (true)
        {
            yield return null;
        }
    }

    public IEnumerator Idle()
    {
        target = null;

        yield return null;
        playerAnim.Play("Idle");
        ChangePlayerState(playerSt.Detect);
    }

    public IEnumerator Detect()
    {
        if(target == null)
        {
            target = BattleManager.Instance.DetectCloseEnemy(this.transform.position);
        }else
        {
            target = BattleManager.Instance.DetectCloseEnemy(target, this.transform.position);
        }
       // Debug.Log("타겟 상태 : " + target != null);
        yield return null;

        if(BattleManager.Instance.PlayerWin)
        {
           // Debug.Log("플레이어 승리");
        }else
        {
            ChangePlayerState(playerSt.Move);
        }
    }

    public IEnumerator Move()
    {
        float tempDis;
        
        playerAnim.Play("Walk");
        do
        {
            if (target == null)
            {
                ChangePlayerState(playerSt.Detect);
                yield break;
            }

            Vector3 targetPos = target.transform.position;
            targetPos.y = this.transform.position.y;

            this.gameObject.transform.LookAt(targetPos);
            if(returnState())
            {
                tempDis = float.MaxValue;
                yield return null;
                continue;
            }

            if (target == null)
            {
              //  Debug.Log("타겟 null");
            }

            Vector3 tempVec = target.transform.position;
            tempVec.y = this.transform.position.y;

            this.transform.position = Vector3.MoveTowards(this.transform.position, tempVec, moveSpeed * Time.deltaTime);
            tempDis = Vector3.Distance(this.transform.position, target.transform.position);
            yield return null;

        } while (tempDis >= player.Range );

        ChangePlayerState(playerSt.Attack);
    }

    public IEnumerator Attack()
    {
        if (attackState)
        {
            //Debug.Log("중복 진입 불가!");
            yield break;
        }
        attackState = true;
        float targetHP = target == null ? 0:target.GetComponent<BattlePlayer>().HP;
        us = target == null? null: target.GetComponent<UnitState>();

        playerAttackST ps =player.Range >= 9 ? playerAttackST.Range : player.Range == 2 ? playerAttackST.Hand : playerAttackST.Melee;
        string attackAnim = "IdleAttack" + ps.ToString();
        if (target == null)
        {
            ChangePlayerState(playerSt.Detect);
            yield break;
        }



        while(targetHP > 0f)
        {
            if (returnState())
            {
                yield return null;
                continue;
            }

            if(target == null)
            {
                ChangePlayerState(playerSt.Detect);
                yield break;

            }
            Vector3 targetPos = target.transform.position;
            targetPos.y = this.transform.position.y;
            targetHP = target.GetComponent<BattlePlayer>().HP;
            us = target.GetComponent<UnitState>();

            this.gameObject.transform.LookAt(targetPos);

            /*  if (returnState())
              {
                  yield return null;
                  continue;
              }*/
            float attackRate = player.AttackSpeed;
            playerAnim.speed = 1 / attackRate;
            if (!us.gameObject.activeSelf)
            {
                ChangePlayerState(playerSt.Detect);
                yield break;
            }
            playerAnim.Play(attackAnim);
            float damage = player.AttackDamage * offset;
            dam = damage;
            if (player.Range >= 9f)
            {
                var temp = Instantiate(rangeOBJ, this.transform.position, Quaternion.identity);
                temp.GetComponent<rangeAttack>().Init(damage, target);
                battleSoundManager.Instance.Arrow();

            }
            else
            {

            }

            //yield return new WaitForSeconds(0.4f);


            targetHP = target.GetComponent<BattlePlayer>().HP;
            yield return new WaitForSeconds(attackRate);
            playerAnim.Play("Idle");
            if (target == null)
            {
                ChangePlayerState(playerSt.Detect);
                yield break;

            }
            if (Vector3.Distance(this.transform.position,target.transform.position) >= player.Range)
            {
                ChangePlayerState(playerSt.Detect);
                yield break;
            }
        }

        //BattleManager.Instance.RemoveEne(target);
        ChangePlayerState(playerSt.Idle);

    }

    public void AdabptDam()
    {
       
        us.GetDam(dam);
        us.idleHitDam();
        battleSoundManager.Instance.Slash();

    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space) && playerSt != playerSt.Skill)
        {
            GameObject beforeTarget = target;
            GameObject testTarget = BattleManager.Instance.changeOtherTarget(target);

            if(testTarget == null)
            {
                target = beforeTarget;
            }else
            {
                target = testTarget;
            }
            ChangePlayerState(playerSt.Move);
        }

        if(player.HP <=0)
        {
            //ChangePlayerState(playerSt.none);
        }
    }

    public void ChangeTarget(GameObject temp)
    {
        target = temp;
    }

    public float ReturnDistance()
    {
        return Vector3.Distance(this.transform.position, target.transform.position);
    }

    public GameObject ReturnNowTarget()
    {
        return target;
    }

    public IEnumerator Die()
    {
        playerAnim.Play("Death");
        yield return new WaitForSeconds(1f);
        battleSoundManager.Instance.GameOver();
        BattleManager.Instance.GameOver();

    }

}
