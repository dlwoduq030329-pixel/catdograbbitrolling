using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rangeAttack : MonoBehaviour
{
    float dam;
    GameObject tar;
  public void Init(float damage,GameObject target)
    {
        tar = target;
        dam = damage;
        StartCoroutine(MovCor());
    }

    public IEnumerator MovCor()
    {
        while (Vector3.Distance(this.transform.position, tar.transform.position) > 0.1f)
        {
            if(tar == null)
            {
                Destroy(this.gameObject);
            }

            this.transform.position = Vector3.MoveTowards(this.transform.position, tar.transform.position, 10 * Time.deltaTime);
            this.transform.LookAt(tar.transform.position);
            yield return null;  
        }
        tar.GetComponent<UnitState>().GetDam(dam);
        tar.GetComponent<UnitState>().idleHitDam();
        Destroy(this.gameObject);
    }
}
