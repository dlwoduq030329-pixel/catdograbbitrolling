using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class stareCamera : MonoBehaviour
{
    [SerializeField]
    GameObject target;
    // Start is called before the first frame update
    void Start()
    {
        target = Camera.main.gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        if (target == null) return;
        this.transform.LookAt(target.transform.position);
    }
}
