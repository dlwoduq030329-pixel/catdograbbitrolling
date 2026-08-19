using System.Collections;
using UnityEngine;

public class cameraChase : MonoBehaviour
{
    Vector3 offset;
    [SerializeField]
    private GameObject target;
    bool isInit = false;
    [SerializeField]
    Vector3[] rotates;
    [SerializeField, Min(0.01f)] float followSmoothTime = 0.15f;
    [SerializeField, Min(0f)] float rotationDelay = 0.12f;
    [SerializeField, Min(0.01f)] float rotationSmoothTime = 0.25f;
    [SerializeField] float lookAtHeight = 1f;
    Vector3 followVelocity;
    float rotationVelocity;
    Coroutine rotationRoutine;
    
    // Start is called before the first frame update
    void Start()
    {
        if (target != null)
        {
            offset = transform.position - target.transform.position;
        }

    }

    // Update is called once per frame
    void Update()
    {
    }

    private void LateUpdate()
    {
        if (!isInit || target == null) return;
        Vector3 desiredPosition = target.transform.position + offset;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime);
        transform.LookAt(target.transform.position + Vector3.up * lookAtHeight);
    }

    public void RotCor(float x)
    {
        if (rotationRoutine != null) StopCoroutine(rotationRoutine);
        rotationRoutine = StartCoroutine(RotateCamera(x));
    }

    IEnumerator RotateCamera(float angle)
    {
        if (target == null) yield break;
        yield return new WaitForSeconds(rotationDelay);

        Vector3 startOffset = offset;
        float currentAngle = 0f;
        rotationVelocity = 0f;
        while (Mathf.Abs(Mathf.DeltaAngle(currentAngle, angle)) > 0.1f)
        {
            currentAngle = Mathf.SmoothDampAngle(
                currentAngle,
                angle,
                ref rotationVelocity,
                rotationSmoothTime);
            offset = Quaternion.Euler(0f, currentAngle, 0f) * startOffset;
            yield return null;
        }
        offset = Quaternion.Euler(0f, angle, 0f) * startOffset;
        rotationRoutine = null;
    }


    public void SetTarget(GameObject temp)
    {
        if (temp == null) return;
        target = temp;
        offset =  this.transform.position - target.transform.position;
        isInit = true;
    }

    public void CamRot()
    {
        //코너로 이동했을경우 카메라 이동 코드
    }

}
