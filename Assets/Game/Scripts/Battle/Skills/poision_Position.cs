using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class poision_Position : MonoBehaviour
{
    UnitState state;
    float damage;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Init(GameObject target)
    {
        state = target.GetComponent<UnitState>();

        float maxHP = target.GetComponent<BattlePlayer>().MaxHp;

        damage = 0.5f + (maxHP * 0.02f);
        StartCoroutine(Position());
    }

    public IEnumerator Position()
    {
        for(int i=0;i<6;i++)
        {
            state.GetDam(damage,damType.magic);
            state.hitPoision();
            yield return new WaitForSeconds(1);
        }
    }
}
