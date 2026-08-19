using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireBall : MonoBehaviour
{

    float damage;
    GameObject target;
    public void Init(GameObject _target,float _damage)
    {
        damage = _damage;
        target = _target;
        StartCoroutine(trail());
    }

    public IEnumerator trail()
    {
        float distance = Vector3.Distance(this.transform.position, target.transform.position);

        while(distance >=0.1f)
        {
            this.transform.position = Vector3.MoveTowards(this.transform.position, target.transform.position, 10f * Time.deltaTime);
            distance = Vector3.Distance(this.transform.position, target.transform.position);
            yield return null;  
        }

        target.GetComponent<UnitState>().GetDam(damage,damType.magic);
        target.GetComponent<UnitState>().debuff(Debuff.Burn,float.MaxValue);
        this.gameObject.SetActive(false);
    }
}
