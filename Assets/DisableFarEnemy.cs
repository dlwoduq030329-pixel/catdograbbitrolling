using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableFarEnemy : MonoBehaviour
{
    [SerializeField]
    SkinnedMeshRenderer[] skin;
    [SerializeField]
    MeshRenderer[] renderers;

    private bool canSee = false;

    private GameObject chaseTarget;

    public void SetTarget(GameObject temp)
    {
        chaseTarget = temp;
        canSee = false;
        foreach (var s in skin)
        {
            s.enabled = false;
        }
        foreach (var s in renderers)
        {
            s.enabled = false;
        }
    }

    private void Update()
    {
        if (chaseTarget == null) return;

        if(Vector3.Distance(chaseTarget.transform.position,this.transform.position) >=10f && !canSee)
        {
            canSee = true;

            foreach(var s in skin)
            {
                s.enabled = true;
            }
            foreach (var s in renderers)
            {
                s.enabled = false;
            }
        }
    }
}
