using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkills : MonoBehaviour
{
    [SerializeField]
    GameObject meteor;
    [SerializeField]
    GameObject explo;
    [SerializeField]
    GameObject fireBall;
    [SerializeField]
    GameObject curse;
    [SerializeField]
    GameObject rainArrow;
    [SerializeField]
    GameObject eruption;
    [SerializeField]
    GameObject iceMagic;
    [SerializeField]
    GameObject light;
    [SerializeField]
    GameObject healArea;
    [SerializeField]
    GameObject poison;
    [SerializeField]
    GameObject holy;
    [SerializeField]
    GameObject[] attackBuff;

    PlayerStateMachine pfsm;
    UnitState myState;
    LineRenderer line;
    BattlePlayer bp;
    Animator playerAnim;
    UnitState us;
    string nowSkill;
    Coroutine activeSkillRoutine;
    Coroutine skillWatchdogRoutine;
    int skillExecutionId;
    const float SkillTimeoutSeconds = 12f;
    bool presentationOnly;
    Coroutine presentationRoutine;

    Collider[] targets;
    float dam;
    private void Awake()
    {
        pfsm = GetComponent<PlayerStateMachine>();
        line = GetComponent<LineRenderer>();
        myState = GetComponent<UnitState>();
        bp = GetComponent<BattlePlayer>();
        playerAnim = GetComponent<Animator>();
        us = GetComponent<UnitState>();
    }

    public Collider[] DrawCollider(Vector3 pos, float range)
    {
        int layerMask = LayerMask.GetMask("Enemy");
        Collider[] hits = Physics.OverlapSphere(pos, range, layerMask);
        return hits;
    }

    public void UseSkill(string skillname)
    {
        if (string.IsNullOrWhiteSpace(skillname))
        {
            Debug.LogError("스킬 이름이 비어 있어 전투 상태를 복구합니다.", this);
            EndSkill();
            return;
        }

        CancelActiveSkill();
        nowSkill = skillname;
        int executionId = ++skillExecutionId;
        activeSkillRoutine = StartCoroutine(RunSkill(skillname, executionId));
        skillWatchdogRoutine = StartCoroutine(SkillWatchdog(executionId));
    }

    public void EndSkill()
    {
        if (presentationOnly) return;
        skillExecutionId++;
        if (skillWatchdogRoutine != null)
        {
            StopCoroutine(skillWatchdogRoutine);
            skillWatchdogRoutine = null;
        }
        activeSkillRoutine = null;
        RemovDraw();
        if (pfsm.PlayerSt == playerSt.Skill)
        {
            playerSt returnState = pfsm.BeforeST == playerSt.Skill || pfsm.BeforeST == playerSt.none
                ? playerSt.Detect
                : pfsm.BeforeST;
            pfsm.ChangePlayerState(returnState);
        }
    }

    IEnumerator RunSkill(string skillname, int executionId)
    {
        Coroutine skillCoroutine = StartCoroutine(skillname);
        if (skillCoroutine == null)
        {
            Debug.LogError($"등록되지 않은 카드 스킬입니다: {skillname}", this);
            if (executionId == skillExecutionId) EndSkill();
            yield break;
        }

        yield return skillCoroutine;
        if (executionId == skillExecutionId && pfsm.PlayerSt == playerSt.Skill)
        {
            EndSkill();
        }
    }

    IEnumerator SkillWatchdog(int executionId)
    {
        yield return new WaitForSeconds(SkillTimeoutSeconds);
        if (executionId == skillExecutionId && pfsm.PlayerSt == playerSt.Skill)
        {
            Debug.LogError($"스킬 제한 시간을 초과해 상태를 복구합니다: {nowSkill}", this);
            CancelActiveSkill();
            EndSkill();
        }
    }

    void CancelActiveSkill()
    {
        if (activeSkillRoutine != null)
        {
            StopCoroutine(activeSkillRoutine);
            activeSkillRoutine = null;
        }
        if (skillWatchdogRoutine != null)
        {
            StopCoroutine(skillWatchdogRoutine);
            skillWatchdogRoutine = null;
        }
        RemovDraw();
    }

    private void OnDisable()
    {
        CancelActiveSkill();
    }

    void Draw(float radius,Vector3 targetPos)
    {
        float angle = 0f;
        int segments = 50;

        line.positionCount = segments + 1;
        line.loop = true;


        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;

            line.SetPosition(i, new Vector3(x, 0, z) + targetPos);

            angle += 360f / segments;
        }
    }

    public void RemovDraw()
    {
        line.positionCount = 0;
    }

    public void GetDamRange()
    {
        if (presentationOnly) return;
        foreach (var col in targets)
        {
            col.gameObject.GetComponent<UnitState>().GetDam(dam);
        }
    }

    public void GetDamSingle()
    {
        if (presentationOnly) return;
        pfsm.Target.GetComponent<UnitState>().GetDam(dam);

    }

    /// <summary>
    /// Renew Battle이 레거시 카드 애니메이션만 재사용할 때 호출한다.
    /// 애니메이션 이벤트가 옛 피해/상태 머신을 다시 실행하지 않도록 잠시 차단한다.
    /// </summary>
    public void PlayPresentationOnly(string stateName, float safetyDuration = 4f)
    {
        if (playerAnim == null || string.IsNullOrWhiteSpace(stateName)) return;
        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        presentationOnly = true;
        playerAnim.Play(stateName);
        if (stateName == "WEAPON_BLESSING") SetAttackBuffVisual(true);
        presentationRoutine = StartCoroutine(EndPresentationOnly(safetyDuration));
    }

    IEnumerator EndPresentationOnly(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, delay));
        SetAttackBuffVisual(false);
        presentationOnly = false;
        presentationRoutine = null;
    }

    private void SetAttackBuffVisual(bool visible)
    {
        if (attackBuff == null) return;
        foreach (GameObject visual in attackBuff)
            if (visual != null) visual.SetActive(visible);
    }

    /// <summary>플레이어 프리팹에 이미 연결된 레거시 VFX 프리팹을 카드 코드로 반환한다.</summary>
    public GameObject GetPresentationPrefab(string cardCode)
    {
        switch (cardCode)
        {
            case "METEOR": return meteor;
            case "EXPLOSION": return explo;
            case "FIRE_BALL": return fireBall;
            case "CURSE_MAGIC": return curse;
            case "RAIN_OF_ARROWS": return rainArrow;
            case "GROUND_ERUPTION": return eruption;
            case "ICE_MAGIC": return iceMagic;
            case "LIGHTNING": return light;
            case "CONSECRATION": return healArea;
            case "POISON_POTION": return poison;
            case "HOLY_ARROW": return holy;
            default: return null;
        }
    }

    public IEnumerator SWING()
    {
        targets = DrawCollider(this.transform.position,bp.Range);
        Draw(1f,this.transform.position);
        //애니메이션 실행
        playerAnim.Play(nowSkill);
        float damage = 6 + (0.1f * DataConfig.playerDatas[0]);
        dam = damage;

        yield return null;
        //EndSkill();
        battleSoundManager.Instance.HardAttack();
    }

    public IEnumerator BODYSLAM()
    {
        yield return StartCoroutine(SetMove());
        playerAnim.Play(nowSkill);

        float damage = 3 + (0.1f * DataConfig.playerDatas[3]);
        dam = damage;
        yield return null;
        //EndSkill();
        battleSoundManager.Instance.HardAttack();

    }

    public IEnumerator WHIRLWIND()
    {
        float damage = 5 + (0.5f * DataConfig.playerDatas[0]);
        dam = damage;
            playerAnim.Play(nowSkill);
        battleSoundManager.Instance.WheelAttack();

        for (int i =0;i<3;i++)
        {
            targets = DrawBoxCollider(this.transform.position, 10, 10, 10);

           /* foreach (var col in temp)
            {
                col.GetComponent<UnitState>().GetDam(dam);
                yield return null;
            }*/
            yield return new WaitForSeconds(0.5f);
        }

        EndSkill();

    }

    public Collider[] DrawBoxCollider(Vector3 pos, float range, float width, float height)
    {



        Vector3 center = this.transform.position;

        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(width * 0.5f, height * 0.5f, range * 0.5f),
            Quaternion.identity,
            LayerMask.NameToLayer("Enemy")
            );
        return hits;
    }

    public IEnumerator HIT_DOWN()
    {
        yield return StartCoroutine(SetMove());
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.HardAttack();

        //애니메이션 실행
        float damage = 4f + (0.5f * DataConfig.playerDatas[0]);
        dam = damage;
        //pfsm.Target.GetComponent<UnitState>().GetDam(damage);
        //pfsm.Target.GetComponent<UnitBase>().CrowdControl(CC.Knockback, 0.2f, this.transform.position, 1f);
        yield return null;
        //EndSkill();
    }

    public IEnumerator SCARECROW()
    {
        yield return null;

        var scare = Instantiate(UnitPool.Instance.unit.unitDatas[0].enemyPrefab,this.transform.position, Quaternion.identity);
        scare.GetComponent<Scareclow>().Init(10f, false);

        Vector3 pos = -this.transform.forward * 2f;

        while(Vector3.Distance(this.transform.position,pos) >= 0.1f)
        {
            this.transform.position = Vector3.MoveTowards(this.transform.position, pos, 30f * Time.deltaTime);
            yield return null;
        }

        EndSkill();
        //pfsm.ChangePlayerState(playerSt.Move);
    }

    public IEnumerator FACE_GUARD()
    {
        playerAnim.Play(nowSkill);

        float shieldAmount = 5f + (0.5f * DataConfig.playerDatas[2]);
        myState.GetShield(shieldAmount, 2f);
        yield return null;
        EndSkill();
    }

    public IEnumerator STALE_JERKY()
    {
        playerAnim.Play(nowSkill);
        us.GetHeal(6);
        EndSkill();
        yield return null;
    }

    public IEnumerator WEIRD_MUSHROOM()
    {
        playerAnim.Play(nowSkill);

        EndSkill();

        float origin = bp.AttackSpeed;

        float upSpeed = origin + (origin * 0.3f);

        bp.AttackSpeed = upSpeed;

        yield return new WaitForSeconds(6f);
        bp.AttackSpeed = origin;
    }

    public IEnumerator HEALING_POTION()
    {
        us.GetHeal(10f);
        EndSkill();
        yield return null;
    }

    public IEnumerator POISON_POTION()
    {
        Instantiate(poison, pfsm.Target.transform.position, Quaternion.identity);
        yield return null;
        EndSkill();
    }

    public IEnumerator WILD_SLASH()
    {
        playerAnim.speed = 5;
        float damage = (3f + (0.3f * DataConfig.playerDatas[0])) * pfsm.Offset;
        for (int i =0;i<5;i++)
        {
            playerAnim.Play("IdleAttackMelee");
            pfsm.Target.GetComponent<UnitState>().GetDam(damage);
            yield return new WaitForSeconds(0.3f);
        }
        playerAnim.speed = 1;

        EndSkill();

    }
    public IEnumerator POWER_STRIKE()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.HardAttack();

        yield return new WaitForSeconds(0.3f);
        pfsm.Target.GetComponent<UnitState>().GetDam(12 + (0.9f * DataConfig.playerDatas[0]));

        yield return null;

    }
    public IEnumerator DIVINE_STRIKE()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.HardAttack();

        yield return new WaitForSeconds(0.3f);
        pfsm.Target.GetComponent<UnitState>().GetDam(10 + (0.6f * DataConfig.playerDatas[1]) + (0.6f * DataConfig.playerDatas[3]),damType.magic);

        yield return null;

    }
    public IEnumerator WEAPON_BLESSING()
    {
        playerAnim.Play(nowSkill);
        attackBuff[0].SetActive(true);
        attackBuff[1].SetActive(true);
        yield return new WaitForSeconds(1.6f);
        EndSkill();
        pfsm.Offset = 1.2f * DataConfig.playerDatas[1];
        yield return new WaitForSeconds(6f);
        attackBuff[0].SetActive(false);
        attackBuff[1].SetActive(false);

        pfsm.Offset = 1;       
        yield return null;

    }
    public IEnumerator HOLY_ARROW()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.Arrow();

        GameObject target = BattleManager.Instance.DetectFarEnemy(this.gameObject);

        var temp = Instantiate(holy, this.transform.position, Quaternion.identity);
        temp.GetComponent<HolyArrow>().Init(temp, 8 + (0.5f * DataConfig.playerDatas[3]));
        yield return new WaitForSeconds(0.5f);
        EndSkill();

        yield return null;

    }

    public IEnumerator SetMove()
    {
        float distance;
        do
        {
            distance = pfsm.ReturnDistance();

            this.transform.position = Vector3.MoveTowards(this.transform.position, pfsm.Target.transform.position,pfsm.MoveSpeed * Time.deltaTime);
            yield return null;

        } while (distance >= bp.Range); // 공격 사거리 만큼
    }

    public IEnumerator VITAL_STRIKE()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.HardAttack();

        yield return null;

        GameObject skillTarget = BattleManager.Instance.DetectFarEnemy(this.gameObject);
        this.gameObject.transform.position = skillTarget.transform.position + new Vector3(1, 1, 1);
        pfsm.ChangeTarget(skillTarget);
        EndSkill();

    }

    public IEnumerator METEOR()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.fireMagic();

        yield return null;

        Instantiate(meteor);

        EndSkill() ;
    }

    public IEnumerator GROUND_ERUPTION()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.GroundMagic();

        yield return null;

        var temp = Instantiate(eruption, this.transform.position, Quaternion.identity);
        temp.GetComponent<ERUPTION>().Init(this.transform.position, 6 + (0.6f * DataConfig.playerDatas[1]));
        EndSkill();
    }
    public IEnumerator EXPLOSION()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.fireMagic();

        yield return null;

        var temp = Instantiate(explo);
        Explosion ex = temp.GetComponent<Explosion>();
        ex.StartExploision(this.transform.position);


        EndSkill();

        yield return new WaitForSeconds(1f);
        Destroy(temp);
    }

    public IEnumerator ICE_MAGIC()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.iceMagic();

        yield return null;

        var temp = Instantiate(iceMagic);
        IceMagic im = temp.GetComponent<IceMagic>();
        im.Init(8 + (0.7f * DataConfig.playerDatas[1]));


        EndSkill();
    }



    public IEnumerator LIGHTNING()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.lightMagic();

        yield return null;


        GameObject[] target = BattleManager.Instance.DetectTwoEnemy();
        
        foreach (GameObject tar in target)
        {
            if (tar == null)
            {
                yield return null;
                continue;
            }    
            var temp =Instantiate(light, tar.transform.position, Quaternion.identity);
            tar.GetComponent<UnitState>().GetDam(12 + (1.3f * DataConfig.playerDatas[1]),damType.magic);
            tar.GetComponent<UnitState>().hitthunder();
           yield return new WaitForSeconds(0.1f);
            Destroy(temp);
        }



        EndSkill();
    }

    public IEnumerator CONSECRATION()
    {
        var temp = Instantiate(healArea,this.transform.position,Quaternion.identity);

        HealArea ha = temp.GetComponent<HealArea>();

        ha.Init(4 + (0.3f * DataConfig.playerDatas[1]),this.transform.position);
        yield return null;
        EndSkill(); 
    }

    public IEnumerator HEALING_TOUCH()
    {
        float heal = 15 + (0.7f * DataConfig.playerDatas[1]) + (0.7f * DataConfig.playerDatas[3]);
        us.GetHeal(heal);
        EndSkill();
        yield return null;
    }

    public IEnumerator HEALING_BREATH()
    {
        float heal = 12 + (1.0f * DataConfig.playerDatas[1]);
        us.GetHeal(heal);
        EndSkill();
        yield return null;
    }
    public IEnumerator CURSE_MAGIC()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.MagicAttack();

        yield return null;
        Vector3 pos = pfsm.Target.transform.position;
        pos.y = 1.5f;
        var temp = Instantiate(curse,pos,Quaternion.identity);
        Curse cu = temp.GetComponent<Curse>();
        cu.Init(pfsm.Target, 3 + (0.3f * DataConfig.playerDatas[1]));
        temp.gameObject.SetActive(true);
        Debug.Log(temp.transform.position);

        EndSkill();
    }

    public IEnumerator RAIN_OF_ARROWS()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.Arrow();

        yield return null;

        var temp = Instantiate(rainArrow,this.transform.position,Quaternion.identity);
        RainArrow ra = temp.GetComponent<RainArrow>();
        ra.Init(pfsm.Target, 7 + (0.5f * DataConfig.playerDatas[2]),this.transform.position);


        EndSkill();
    }


    public IEnumerator FIRE_BALL()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.fireMagic();

        yield return null;

        var temp = Instantiate(fireBall,this.transform.position,Quaternion.identity);
        FireBall fb = temp.GetComponent<FireBall>();
        fb.Init(pfsm.Target, 6 + (1 * DataConfig.playerDatas[1]));

        EndSkill();
    }
    public IEnumerator FINISHING_BLOW()
    {
        GameObject skillTarget = pfsm.ReturnNowTarget();
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.HardAttack();

        float nowEnemyHP = skillTarget.GetComponent<BattlePlayer>().HP;

        if(nowEnemyHP <= 15f)
        {
            skillTarget.GetComponent<UnitState>().GetDam(nowEnemyHP);
        }
        else
        {
            float damage = 5 + (float)(0.5 * DataConfig.playerDatas[0]);
            skillTarget.GetComponent<UnitState>().GetDam(damage);
        }

        yield return null;
    }

    public IEnumerator BLUNT_STRIKE()
    {
        playerAnim.Play(nowSkill);
        battleSoundManager.Instance.HardAttack();

        float damage = 15 + (float)(1.1 * DataConfig.playerDatas[0]);
        float myDamage = Mathf.RoundToInt(damage / 3);
        GameObject skillTarget = pfsm.ReturnNowTarget();
        dam = damage;
        
        //skillTarget.GetComponent<UnitState>().GetDam(damage);
        us.GetDam(myDamage);
        yield return null;

    }
}
