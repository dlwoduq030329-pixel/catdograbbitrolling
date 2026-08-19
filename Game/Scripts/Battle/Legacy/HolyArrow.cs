using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HolyArrow : MonoBehaviour
{
    GameObject target;
    float damage;
    public void Init(GameObject _target,float _damage)
    {
        damage = _damage;
        target = _target;
        StartCoroutine(Trail());
    }

    public IEnumerator Trail()
    {
        while(Vector3.Distance(this.transform.position,target.transform.position) >= 0.1f)
        {
            this.gameObject.transform.LookAt(target.transform.position);
            this.transform.position = target.transform.position;
            yield return null;
        }

        UnitState us = target.GetComponent<UnitState>();

        us.GetDam(damage, damType.magic);
        us.hitholy();

        Destroy(this.gameObject);
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
