using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class ERUPTION : MonoBehaviour
{

    [SerializeField]
    Material transparent;
    GameObject hitBox;
    float damage;
    Collider[] targets;

    public void Init(Vector3 pos,float Dam)
    {
        this.transform.position = pos;
        damage = Dam;

        targets = DrawBoxCollider(pos, 20, 20, 5);

        StartCoroutine(threeTimeDamage());
    }

    public IEnumerator threeTimeDamage()
    {
        for(int i =0; i<3;i++)
        {
            foreach(Collider col in targets)
            {
                col.GetComponent<UnitState>().GetDam(damage,damType.magic);
            }
            yield return new WaitForSeconds(1);
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
