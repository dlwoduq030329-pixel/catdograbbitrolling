using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skydomRot : MonoBehaviour
{
    [SerializeField]
    float rotSpeed;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Vector3.up, rotSpeed * Time.deltaTime);

        this.transform.position += new Vector3(0,Mathf.Sin(Time.time)/100, 0)*Random.Range(-1,2);
        Vector3 tempPos = Camera.main.transform.position;
        //tempPos.y = this.transform.position.y;
        this.transform.position = tempPos;
    }


}
