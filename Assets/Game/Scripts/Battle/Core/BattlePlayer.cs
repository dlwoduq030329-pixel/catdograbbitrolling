using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattlePlayer : MonoBehaviour
{
    float attackSpeed;
    float attackDamage;
    float apPerHeal;
    [SerializeField]
    float hp;
    float maxHp;
    float attackRange;
    float myAP;
    float shieldHP;

    public float AttackSpeed
    {
        get { return attackSpeed; }
        set { attackSpeed = value; }
    }
    public float AttackDamage => attackDamage;
    public float ApPerHeal => apPerHeal;
    public float AttackRagne => attackRange;
    public float HP {
        get { return hp; }
        set { hp = value; }
    }
    public float MaxHp => maxHp;
    public float MyAp => myAP;
    public float ShieldHP
    {
        get { return shieldHP; }
        set { shieldHP = value; }
    }

    private playerAttackST ps;
    public playerAttackST Ps
    {
        get { return ps; }
        set { ps = value; }
    }

    float skillCool = 99999;
    public float SkillCool => skillCool;


    private float range;
    public float Range => range;    

    [SerializeField]
    LinkHP link;
    [SerializeField]
    bool isPlayer = false;
    [SerializeField]
    bool isBoss = false;
    [SerializeField]
    GameObject bossSlider;
    //

    public void SetPlayer()
    {
        isPlayer = true;
    }

    public void rangeSet(float temp)
    {
        range = temp;
    }
    private void Update()
    {
        if (link == null) return;
        link.UpdateHP(hp, maxHp);
    }

    public void Init()
    {
        if (!isPlayer) return;
        attackSpeed = 1.25f;
        int attackStat = ps == playerAttackST.Range
            ? DataConfig.GetCombatStat(2, playerAttackST.Range)
            : DataConfig.GetCombatStat(0, playerAttackST.Melee);
        attackDamage = 3f + (0.3f * attackStat);
        apPerHeal = 1f + (0.2f * DataConfig.playerDatas[2]);
        hp = 15 + (5 * DataConfig.playerDatas[3]);
        maxHp = hp;

      

        ShieldSet();
        if (!isPlayer) return;
    }

    public void EnemyInit(float _hp, float damage,float _range,float speed)
    {
        range = _range;
        attackDamage = damage;
        hp = _hp;
        attackSpeed = speed;
        maxHp = hp;

        if(isBoss)
        {
            var temp = Instantiate(bossSlider);
            link = temp.GetComponent<LinkHP>();
            skillCool = 6;
        }
    }

    public void Init(float _hp,LinkHP _link)
    {
        if (!isPlayer) return;
        maxHp = _hp;
        hp = _hp;
        link = _link;
    }

    public void ShieldSet(float amount = 0)
    {
        shieldHP = amount;
    }

    public void ChangePlayerST()
    {

    }

    public void StartHealAp()
    {
        StartCoroutine(nameof(HealAP));

    }

 

    public IEnumerator HealAP()
    {
        while(true)
        {
            
            AddAp();
            yield return new WaitForSeconds(1f);
        }
    }

    public void UseAp(float x)
    {
        myAP -= x;
    }

    public void AddAp()
    {
        
        myAP += apPerHeal;
        if (myAP >= 10f)
        {
            myAP = 10f;

        }
    }

    public void apSet(float x)
    {
        if (myAP >= 10)
        {
            myAP = 10;
            return;
        }
        myAP += x;
    }

    public void EnemyInit(int x)
    {
        attackSpeed = UnitPool.Instance.unit.unitDatas[x].attackSpeed;
        attackDamage = UnitPool.Instance.unit.unitDatas[x].attackDamage;
        hp = UnitPool.Instance.unit.unitDatas[x].enemyHP;
        attackRange = UnitPool.Instance.unit.unitDatas[x].attackRange;
        skillCool = UnitPool.Instance.unit.unitDatas[x].skillCool;
        maxHp = hp;
    }

    public void ChangeEnemyST()
    {

    }
}
