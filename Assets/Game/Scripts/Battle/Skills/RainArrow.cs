using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RainArrow : MonoBehaviour
{
    [SerializeField]
    Material transparent;
    //GameObject hitBox;
    GameObject target;
    float damage;

    Collider[] targets;

    public void Init(GameObject _target, float _damage,Vector3 pos)
    {
        target = _target;
        damage = _damage;

        targets = DrawBoxCollider(pos, 20, 20, 5);
        this.transform.position = pos;
        Rain();
    }


    public void Rain()
    {
        foreach (Collider c in targets)
        {
            c.GetComponent<UnitState>().GetDam(damage);
        }

        //hitBox.SetActive(false);
        this.gameObject.SetActive(false);
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

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
