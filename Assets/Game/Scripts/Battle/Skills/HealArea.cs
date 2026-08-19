using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealArea : MonoBehaviour
{
    Collider[] targets;
    float heal;
    Vector3 thisObjPos;

    private Vector3 gizmoCenter;
    private Vector3 gizmoSize;
    private bool drawGizmo;

    public void Init(float _heal,Vector3 pos)
    {
        thisObjPos = pos;
        heal = _heal;

        StartCoroutine(healCor());
    }

    public IEnumerator healCor()
    {
        for(int i =0;i<4;i++)
        {
            targets = DrawBoxCollider(thisObjPos, 64, 64, 5);

            foreach(Collider c in targets)
            {
                if (c.TryGetComponent<UnitState>(out UnitState state)) state.GetHeal(heal);
                yield return null;
            }
            yield return new WaitForSeconds(1f);
        }
            Destroy(this.gameObject);
    }
    public Collider[] DrawBoxCollider(Vector3 pos, float range, float width, float height)
    {



        Vector3 center = this.transform.position;
        gizmoCenter = center;
        gizmoSize = new Vector3(width, height, range);
        drawGizmo = true;
        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(width * 0.5f, height * 0.5f, range * 0.5f),
            Quaternion.identity,
            LayerMask.GetMask("Player")
            );
        return hits;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(gizmoCenter, gizmoSize);
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
