using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoreLookPlayerPos : MonoBehaviour
{
    [SerializeField]
    GameObject player;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("body");
        Vector3 pos = player.transform.position;
        pos.y = this.transform.position.y;
        this.gameObject.transform.LookAt(pos);
    }

    // Update is called once per frame
}
