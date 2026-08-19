using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class IceMagic : MonoBehaviour
{
    Collider[] targets;

    float damage;
    [SerializeField]
    GameObject magic;
    [SerializeField]
    GameObject crystal;

    public void Init(float dam)
    {
        damage = dam;

        targets = DrawBoxCollider(Vector3.zero, 1000, 1000, 1000);


       magic.SetActive(true);
        Invoke(nameof(Crystal), 1.5f);
    }

    public void Crystal()
    {
        magic.SetActive(false);
        crystal.SetActive(true);
        foreach (Collider c in targets)
        {
            UnitState temp = c.gameObject.GetComponent<UnitState>();
            temp.GetDam(damage, damType.magic);
            temp.debuff(Debuff.Cold, 4f);
        }
        Invoke("DisThis", 2f);

    }






    public void DisThis()
    {
        Destroy(this.gameObject);
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
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
