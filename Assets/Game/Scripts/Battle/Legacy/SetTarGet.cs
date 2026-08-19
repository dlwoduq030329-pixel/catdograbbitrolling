using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTarGet : MonoBehaviour
{
    [SerializeField]
    GameObject targeting;

    public bool isdie = false;
    // Start is called before the first frame update
    void Start()
    {
        isdie = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetTarget()
    {
        targeting.SetActive(true);
    }

    public void UnTarget()
    {
        targeting.SetActive(false);

    }

    public void Die()
    {
        isdie  = true;
    }
}
