using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class movTest : MonoBehaviour
{
    [SerializeField]
    GameObject target;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float dis = Vector3.Distance(this.transform.position, target.transform.position);

        if(dis > 1f)
        {

        this.transform.position = Vector3.MoveTowards(this.transform.position, target.transform.position, 10 * Time.deltaTime);
        }
    }
}
