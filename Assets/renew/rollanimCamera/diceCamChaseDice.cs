using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class diceCamChaseDice : MonoBehaviour
{
    [Tooltip("3D 주사위를 UI에 띄우기 위해 사용하는 카메라가 주사위를 따라가게 하기위한 코드.")]
    [Header("코드 설명용 변수. 마우스를 올려 확인.")]
    [SerializeField]
    bool CODE_EXPLAIN;

    [Header("타겟 주사위.")]
    [SerializeField]
    GameObject Target;
    [Header("주사위와 카메라의 위치 보정값. 추가적으로 카메라의 시점은 orthographic")]
    [SerializeField]
    Vector3 offset;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.position = Target.transform.position + offset;
    }
}
