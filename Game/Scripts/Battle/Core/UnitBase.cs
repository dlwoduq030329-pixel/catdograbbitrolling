using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CC
{
    Taunt,
    Slow,
    Stun,
    Knockback,

}

public enum Debuff
{
    Scar,
    Shred,
    Nervous,
    Burn,
    Cold
}

public abstract class UnitBase : MonoBehaviour
{
    public Dictionary<CC,float> CCDic= new Dictionary<CC,float>();
    public Dictionary<Debuff,float> DebuffDic =new Dictionary<Debuff, float> { };
    float slowrate;
    float forcerate;
    Vector3 target;
    bool canFire = true;

    private void OnDisable()
    {
        canFire = false;

    }

    public void CrowdControl(CC crowd,float Time,Vector3 targetPos = default,float power = 0f)
    {
        if(crowd == CC.Slow)
        {
            slowrate = power;
        }
        if(crowd == CC.Knockback)
        {
            forcerate = power;
        }
        target = targetPos;
        if(CCDic.ContainsKey(crowd))
        {
            CCDic[crowd] = Time;
        }
        else
        {
            CCDic.Add(crowd, Time);
            StartCoroutine(crowd.ToString());
        }
    }

    public void debuff(Debuff de, float Time)
    {
        if (DebuffDic.ContainsKey(de))
        {
            DebuffDic[de] = Time;
        }
        else
        {
            DebuffDic.Add(de, Time);
            StartCoroutine(de.ToString());
        }
    }


    public virtual void StartCC(CC cc, Vector3 temp =default,float power = 0f)
    {

    }

    public virtual void EndCC(CC cc)
    {

    }

    public virtual void StartDebuff(Debuff de)
    {

    }

    public virtual void EndDebuff(Debuff de)
    {

    }

    public void DebuffSkill(Debuff de, float Time)
    {

    }

    private IEnumerator Taunt()
    {
        float time;
        StartCC(CC.Taunt);
        do
        {
            CCDic.TryGetValue(CC.Taunt, out time);
            time -= Time.deltaTime;
            CCDic[CC.Taunt] = time;
            yield return null;
        }
        while (time >= 0.1f);
        CCDic.Remove(CC.Taunt);
        EndCC(CC.Taunt);
    }

    private IEnumerator Slow()
    {
        BattlePlayer pb = GetComponent<BattlePlayer>();
        float time;
        StartCC(CC.Slow);
        float origin = pb.AttackSpeed;
        pb.AttackSpeed *= slowrate;
        do
        {
            CCDic.TryGetValue(CC.Taunt, out time);
            time -= Time.deltaTime;
            CCDic[CC.Taunt] = time;
            yield return null;
        }
        while (time >= 0.1f);
        pb.AttackSpeed =origin;

        CCDic.Remove(CC.Slow);
        EndCC(CC.Slow);
    }
    private IEnumerator Stun()
    {
        float time;
        StartCC(CC.Stun);
        do
        {
            
            CCDic.TryGetValue(CC.Stun, out time);
            Debug.Log("기절시간 : " + time);
            time -= Time.deltaTime;
            CCDic[CC.Stun] = time;
            yield return null;
        }
        while (time >= 0.1f);
        CCDic.Remove(CC.Stun);
        EndCC(CC.Stun);
    }
    private IEnumerator Knockback()
    {
        float time;
        StartCC(CC.Knockback,target,forcerate);
        do
        {
            if (!this.gameObject.activeSelf) break;
            CCDic.TryGetValue(CC.Taunt, out time);
            time -= Time.deltaTime;
            CCDic[CC.Taunt] = time;
            yield return null;
        }
        while (time >= 0.1f);
        CCDic.Remove(CC.Knockback);
        EndCC(CC.Knockback);
    }

    private IEnumerator Scar()
    {
        float time;
        StartDebuff(Debuff.Scar);
        do
        {
            DebuffDic.TryGetValue(Debuff.Scar, out time);
            time -= Time.deltaTime;
            DebuffDic[Debuff.Scar] = time;
            yield return null;
        }
        while (time >= 0.1f);
        DebuffDic.Remove(Debuff.Scar);
        EndDebuff(Debuff.Scar);

    }

    private IEnumerator Shred()
    {
        float time;
        StartDebuff(Debuff.Shred);
        do
        {
            DebuffDic.TryGetValue(Debuff.Shred, out time);
            time -= Time.deltaTime;
            DebuffDic[Debuff.Shred] = time;
            yield return null;
        }
        while (time >= 0.1f);
        DebuffDic.Remove(Debuff.Shred);
        EndDebuff(Debuff.Shred);

    }

    private IEnumerator Nervous()
    {
        float time;
        StartDebuff(Debuff.Nervous);
        do
        {
            DebuffDic.TryGetValue(Debuff.Nervous, out time);
            time -= Time.deltaTime;
            DebuffDic[Debuff.Nervous] = time;
            yield return null;
        }
        while (time >= 0.1f);
        DebuffDic.Remove(Debuff.Nervous);
        EndDebuff(Debuff.Nervous);

    }

    private IEnumerator Burn()
    {

        float time;
        StartDebuff(Debuff.Burn);
        do
        {
            if(!this.gameObject.activeSelf||!canFire)
            {
                DebuffDic.Remove(Debuff.Burn);
                EndDebuff(Debuff.Burn);
                yield break;
            }
            DebuffDic.TryGetValue(Debuff.Burn, out time);
            time -= Time.deltaTime;
            DebuffDic[Debuff.Burn] = time;
            yield return null;
        }
        while (time >= 0.1f);
        DebuffDic.Remove(Debuff.Burn);
        EndDebuff(Debuff.Burn);

    }

    private IEnumerator Cold()
    {
        float time;
        StartDebuff(Debuff.Cold);
        do
        {
            DebuffDic.TryGetValue(Debuff.Cold, out time);
            time -= Time.deltaTime;
            DebuffDic[Debuff.Cold] = time;
            yield return null;
        }
        while (time >= 0.1f);
        DebuffDic.Remove(Debuff.Cold);
        EndDebuff(Debuff.Cold);

    }

    public virtual void GetDam(float x, damType type = damType.physical)
    {

    }

    public virtual void GetHeal(float x)
    {

    }

    public virtual void GetShield(float x,float time)
    {

    }



    public void StatUp(int index,float time,float percent)
    {
        StartCoroutine(StatUpCor(index, time, percent));
    }

    public IEnumerator StatUpCor(int index, float time,float percent)
    {
        int temp = DataConfig.playerDatas[index];
        int offset = Mathf.FloorToInt(temp * percent);
        DataConfig.playerDatas[index] += offset;
        yield return new WaitForSeconds(time);
        DataConfig.playerDatas[index] = temp;
    }


}
