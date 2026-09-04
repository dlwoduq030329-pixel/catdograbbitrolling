using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageManager : MonoBehaviour
{
    private static DamageManager instance = null;
    public static DamageManager Instance => instance;

    [SerializeField]
    GameObject damageText;
    [SerializeField]
    Vector3 testPos;

    private void Awake()
    {
        if(instance == null)
        {
             instance = this;
        }
    }

    public void testObj()
    {
        int x = Random.Range(0, 2);
        int damage = Random.Range(0, 1000);
        int critical = Random.Range(0, 2);
        int isMiss = Random.Range(0, 2);

        InstanDamageText(damage, (damType)x, critical == 0 ? true : false, isMiss == 0 ? true : false,testPos);
       
    }

    public void InstanDamageText(float damage,damType dam,bool isCritical,bool isMiss,Vector3 pos)
    {
        var textobj = Instantiate(damageText, pos, Quaternion.identity);
        textobj.GetComponent<DamageText>().DamageTextInit(damage, dam, isCritical, isMiss);
    }
}
