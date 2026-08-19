using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    [SerializeField]
    Material transparent;
    Collider[] targets;
    GameObject box;
    float damage;
    public void StartExploision(Vector3 pos)
    {
        damage = 1 + (float)(3.0 * DataConfig.playerDatas[1]);
        targets = DrawBoxCollider(pos, 20,20 , 5);

        foreach (Collider c in targets)
        {
            if(c.gameObject.layer==LayerMask.NameToLayer("Enemy"))
            {
                c.gameObject.GetComponent<UnitState>().GetDam(damage, damType.magic);
                c.gameObject.GetComponent<UnitState>().hitFire();

            }
            else if(c.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                c.gameObject.GetComponent<UnitState>().CrowdControl(CC.Stun, 3f);
            }else
            {
                continue;   
            }

        }
    }
    public Collider[] DrawBoxCollider(Vector3 pos, float range, float width, float height)
    {



        Vector3 center = this.transform.position;

        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(width * 0.5f, height * 0.5f, range * 0.5f),
            Quaternion.identity
            );
        GameObject hitBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box = hitBox;
        Destroy(hitBox.GetComponent<BoxCollider>());
        Collider col = hitBox.GetComponent<Collider>();

        if (col != null)
        {
            col.enabled = false;
        }
        hitBox.GetComponent<MeshRenderer>().material = transparent;

        hitBox.transform.position = center;
        hitBox.transform.localScale = new Vector3(width, height, range) * 0.5f;
        return hits;
    }
    // Start is called before the first frame update
    void Start()
    {
        //StartExploision(Vector3.zero);
    }


    private void OnDisable()
    {
        Destroy(box);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
