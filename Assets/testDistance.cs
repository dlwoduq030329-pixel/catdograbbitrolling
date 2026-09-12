using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testDistance : MonoBehaviour
{
   List<Vector3> poses = new List<Vector3>();

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.P))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                poses.Add(hit.transform.gameObject.transform.position);

                if(poses.Count == 2)
                {
                    Debug.Log(Vector3.Distance(poses[0], poses[1]));
                    poses.Clear();
                }
            }
        }
    }
}
