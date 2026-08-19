using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Curse : MonoBehaviour
{
    GameObject target;
    float damage;

    public void Init(GameObject _target,float _damage)
    {
        target = _target;
        damage = _damage;

        //Vector3 pos = target.transform.position;
       // pos.y = 0;
        //this.gameObject.transform.position = pos;


        Invoke(nameof(adabt), 1f);
        Invoke(nameof(setDis), 1.2f);

    }

    public void adabt()
    {
        UnitState us = target.GetComponent<UnitState>();

        us.GetDam(damage);
        us.hitCurse();
        us.debuff(Debuff.Shred, float.MaxValue);
        us.debuff(Debuff.Nervous, float.MaxValue);
    }

    public void setDis()
    {
        this.gameObject.SetActive(false);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
