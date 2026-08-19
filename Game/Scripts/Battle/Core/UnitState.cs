using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum damType
{
    physical,
    magic
}

public class UnitState : UnitBase
{
    private bool isTaunt = false;
    private bool isSlow = false;
    private bool isStun = false;
    private bool isKnockback = false;

    private bool isScar = false;
    private bool isShred = false;
    private bool isNervous = false;
    private bool isBurn = false;
    private bool isCold = false;

    public bool IsTaunt => isTaunt;
    public bool IsSlow => isSlow;
    public bool IsStun => isStun;
    public bool IsKnockback => isKnockback;
    public bool IsBurn => isBurn;
    public bool IsCold => isCold;

    public bool IsScar => isScar;
    public bool IsShred => isShred;
    public bool IsNervous => isNervous;

    BattlePlayer battlePlayer;

    [SerializeField]
    bool isPlayer;
    [SerializeField]
    GameObject healPreafab;
    GameObject heal;
    [SerializeField]
    GameObject buffOBJ;
    [SerializeField]
    GameObject debuffOBJ;
    [SerializeField]
    GameObject shieldEffect;

    [SerializeField]
    GameObject IdleHit;
    [SerializeField]
    GameObject fireHit;
    [SerializeField]
    GameObject holyHit;
    [SerializeField]
    GameObject thunderHit;
    [SerializeField]
    GameObject positionHit;
    [SerializeField]
    GameObject curseHit;

    GameObject idlehit;

    List<GameObject> idleEffects = new List<GameObject>();

    PlayerStateMachine pfsm;
    private void Awake()
    {
        pfsm = GetComponent<PlayerStateMachine>();
        battlePlayer = GetComponent<BattlePlayer>();
    }

    public bool isNoCCorDebuff()
    {
        if(!isTaunt&&
            !isSlow&&
            !isStun&&
            !isKnockback&&
            !isScar&&
            !isShred&&
            !isNervous&&
            !isBurn&&
            !isCold)
        {
            return true;
        }else
        {
            return false;
        }
    }


    public (int,bool) returnDisableIndex()
    {
        int returnIndex = 0;
        bool canreturn = false;
        for(int i =0; i<idleEffects.Count;i++)
        {
            if (!idleEffects[i].activeSelf)
            {
                returnIndex =  i;
                canreturn = true;
                break;
            }
        }
        return (returnIndex,canreturn);
    }

   

    public void idleHitDam()
    {
         var (i1,i2) = returnDisableIndex();

        if(i2)
        {
            idleEffects[i1].gameObject.SetActive(true);
        }else
        {
            idleEffects.Add(Instantiate(IdleHit, this.transform));
        }

        
    }

    public void hitFire()
    {
        Instantiate(fireHit, this.transform);
    }

    public void hitthunder()
    {
        Instantiate(thunderHit, this.transform);
    }
    public void hitholy()
    {
        Instantiate(holyHit, this.transform);
    }

    public void hitCurse()
    {
        Instantiate(curseHit, this.transform);
    }
    public void hitPoision()
    {
        Instantiate(positionHit, this.transform);
    }

    public override void StartCC(CC cc,Vector3 temp,float power)
    {
        debuffOBJ.SetActive(true);
       switch(cc)
        {
            case CC.Taunt:
                {
                    isTaunt = true;
                }
                break;  
            case CC.Slow:
                {
                    isSlow = true;
                }
                break;
            case CC.Stun:
                {
                    isStun = true;
                }
                break;
            case CC.Knockback:
                {
                    isKnockback = true;
                    Vector3 tempPos = (this.transform.position - temp).normalized;
                    Rigidbody rb = GetComponent<Rigidbody>();
                    rb.AddForce(tempPos * power, ForceMode.Impulse);
                }
                break;
        }

    }

    public override void EndCC(CC cc)
    {
        switch (cc)
        {
            case CC.Taunt:
                {
                    isTaunt = false;
                }
                break;
            case CC.Slow:
                {
                    isSlow = false;
                }
                break;
            case CC.Stun:
                {
                    isStun = false;
                }
                break;
            case CC.Knockback:
                {
                    isKnockback = false;
                }
                break;
        }

        if(isNoCCorDebuff())
        {
            debuffOBJ.gameObject.SetActive(false);
        }


    }

    public override void StartDebuff(Debuff de)
    {
        debuffOBJ.gameObject.SetActive(true);
        switch(de)
        {
            case Debuff.Scar:
                {
                    isScar = true;
                }
                break;
            case Debuff.Shred:
                {
                    isShred = true;
                }
                break;
            case Debuff.Nervous:
                {
                    isNervous = true;
                }
                break;
            case Debuff.Burn:
                {
                    isBurn = true;
                    break;
                }
            case Debuff.Cold:
                {
                    isCold = true;
                }
                break;
        }
    }

    public override void EndDebuff(Debuff de)
    {
        switch (de)
        {
            case Debuff.Scar:
                {
                    isScar = false;
                }
                break;
            case Debuff.Shred:
                {
                    isShred = false;
                }
                break;
            case Debuff.Nervous:
                {
                    isNervous = false;
                }
                break;
            case Debuff.Burn:
                {
                    isBurn = false;
                }
                    break;
            case Debuff.Cold:
                {
                    isCold = false;
                }
                break;
        }

        if (isNoCCorDebuff())
        {
            debuffOBJ.gameObject.SetActive(false);
        }

    }

    public override void GetHeal(float x)
    {
        battleSoundManager.Instance.Heal();

        if (heal == null)
        {
            heal = Instantiate(healPreafab, this.transform.position, Quaternion.identity);
        }else
        {
            heal.transform.position = this.transform.position;
            heal.gameObject.SetActive(true);
        }

        if(isScar)
        {
            float ScarX = x * 0.6f;
            battlePlayer.HP += ScarX;
        }else
        {
            battlePlayer.HP += x;
        }

        if(battlePlayer.HP > battlePlayer.MaxHp)
        {
            battlePlayer.HP = battlePlayer.MaxHp;
        }
        Invoke(nameof(DisableHeal), 0.5f);
        base.GetHeal(x);
    }

    public void DisableHeal()
    {
        heal.gameObject.SetActive(false);
    }

    public override void GetDam(float x,damType type = damType.physical)
    {
        battleSoundManager.Instance.getDam();

        float temp;
        if(!isPlayer)
        {
           // Debug.Log("아야!");
        }
        if(type == damType.physical&& isShred)
        {
            x *= 1.3f;
        }

        if (type == damType.magic && isNervous)
        {
            x *= 1.3f;
        }
        if (battlePlayer.ShieldHP > 0)
        {
            temp = battlePlayer.ShieldHP - x;
            
            if(temp < 0)
            {
                battlePlayer.ShieldHP = 0;
                battlePlayer.HP += temp;
            }
            else
            {
                battlePlayer.ShieldHP -= x;
            }

        }else
        {
            battlePlayer.HP -= x;
            if (battlePlayer.HP <= 0)
            {
                if (isPlayer)
                {
                    PlayerStateMachine sm;

                    if(TryGetComponent<PlayerStateMachine>(out sm))
                    {
                        sm.ChangePlayerState(playerSt.Die);
                    }else
                    {
                       // BattleManager.Instance.enemyChangeTarget(BattleManager.Instance.Player);
                       // Destroy(this.gameObject);
                    }
                }
                else
                {
                    NullDebuff();

                    EnemyStateMachine tempesm;

                    if(TryGetComponent<EnemyStateMachine>(out tempesm))
                    {
                        tempesm.ChangePlayerState(playerSt.Die);
                    }


                    //GetComponent<EnemyBattleAnim>().Die();
                }
            }
        }

      
        base.GetDam(x);
    }

    public void NullDebuff()
    {
        for(int i =0; i< 5;i++)
        {
            EndDebuff((Debuff)i);
        }
        for(int j=0;j<4;j++)
        {
            EndCC((CC)j);
        }
    }

    public void DieDisable()
    {
        //this.gameObject.SetActive(false);
    }

    public override void GetShield(float x, float time)
    {
        shieldEffect.gameObject.SetActive(true);
        battlePlayer.ShieldSet(x);
        Invoke(nameof(EndShield), time);
    }

    public void EndShield()
    {
        battlePlayer.ShieldSet();
        shieldEffect.gameObject.SetActive(false);

    }

    public void Block()
    {

    }

    public IEnumerator Burn()
    {
        while(isBurn)
        {
            GetDam(1);
            yield return new WaitForSeconds(1);
        }
    }    


}
