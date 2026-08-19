using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraChase : MonoBehaviour
{
    GameObject targetPlayer;
    [SerializeField]
    Vector3 offset;
    [SerializeField]
    float chaseOffset;
    bool isTargeted = false;

    private void LateUpdate()
    {
        if (!isTargeted) return;

        this.gameObject.transform.position = Vector3.Lerp(this.transform.position, targetPlayer.transform.position + offset, chaseOffset * Time.deltaTime);

    }

    public void InitTarget(GameObject targetTemp)
    {
        targetPlayer = targetTemp;

        isTargeted = true;
        Camera.main.orthographic = false;

        this.transform.rotation = Quaternion.Euler(90, 0, 0);
    }
}
