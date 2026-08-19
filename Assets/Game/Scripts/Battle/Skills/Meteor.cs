using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Meteor : MonoBehaviour
{
    Collider[] targets;
    float damage;
    [SerializeField]
    GameObject effect;
    [SerializeField]
    Material transparent;

    GameObject hitBox;


    List<GameObject> hiteffect = new List<GameObject>();
    // Start is called before the first frame update
    void Start()
    {
        targets = DrawBoxCollider(this.transform.position, 100, 100, 5);
        damage = 20 + (1.0f * DataConfig.playerDatas[1]);

        Debug.Log(targets.Length);

        effectPool();
        StartCoroutine(meteorStart());
    }

    public Collider[] DrawBoxCollider(Vector3 pos, float range, float width, float height)
    {


        
        Vector3 center = this.transform.position;

        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(width * 0.5f, height * 0.5f, range * 0.5f),
            Quaternion.identity,
            LayerMask.GetMask("Enemy")
        );
        return hits;
    }

    public IEnumerator meteorStart()
    {
        for (int i = 0; i<3;i++)
        {
            foreach (Collider col in targets)
            {
                col.gameObject.GetComponent<UnitState>().GetDam(damage/3);
                col.gameObject.GetComponent<UnitState>().hitFire();
                spawnEffect(col.gameObject.transform.position);
            }
            yield return new WaitForSeconds(1f);
        }
        EffectDisable();
        this.gameObject.SetActive(false);
    }

    public void effectPool()
    {
        for(int i =0;i<6;i++)
        {
            hiteffect.Add(Instantiate(effect));

            hiteffect[i].gameObject.SetActive(false);
        }
    }


    public void EffectDisable()
    {
        for (int i = 0; i < 6; i++)
        {

            hiteffect[i].gameObject.SetActive(false);
        }
    }
    public void spawnEffect(Vector3 pos)
    {
        foreach(var eff in hiteffect)
        {
            if (eff.gameObject.activeSelf) continue;
            else
            {
                eff.gameObject.SetActive(true);
                
                eff.transform.position = pos;
                break;
            }
        }
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
