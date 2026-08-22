using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// walkParticle 애니메이션 사용시 할당. 캐릭터 좌 우 발 위치에 FootParticle 2개를 넣은 뒤 left right 할당. 애니메이션 key추가후 함수 할당.
/// </summary>
public class WalkParticle : MonoBehaviour //WalknParticle 애니메이션 사용시 할당
{
    [Tooltip("WalknParticle 애니메이션 사용시 할당. 캐릭터 좌 우 발 위치에 FootParticle 2개를 넣은 뒤 left right 할당. 애니메이션 key추가후 함수 할당.")]
    [Header("코드 설명용 변수. 마우스를 올려 확인.")]
    [SerializeField]
    bool CODE_EXPLAIN;
    [SerializeField]
    ParticleSystem left;
    [SerializeField]
    ParticleSystem right;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //this.transform.position += 5*Vector3.forward * Time.deltaTime;
    }

    public void LeftFoot()
    {
        left.Play();
        Debug.Log("왼발!");
    }
    public void RightFood()
    {
        right.Play();
        Debug.Log("오른발!");

    }
}
