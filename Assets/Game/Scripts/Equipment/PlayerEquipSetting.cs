using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEquipSetting : MonoBehaviour
{
    [SerializeField]
    Vector3[] rotPos;
    [SerializeField]
    GameObject[] weapons;

    weaponSet weaponSet;

    int leftindex=0;
    int rightindex=0;
    int bodyindex = 0;
    int headindex = 0;

    // UpdateWeapon()이 어디서도 호출되지 않아 이 미리보기 모델은 장비를 구매해도
    // 항상 기본값(0번)만 표시하고 있었다. EquipStore.Init()과 같은 패턴으로,
    // 이 오브젝트(캐릭터 정보 패널의 미리보기 모델)가 활성화될 때마다 최신
    // DataConfig 장착 인덱스로 갱신한다.
    private void OnEnable()
    {
        UpdateWeapon();
    }

    public void UpdateWeapon()
    {
        weaponSet = GetComponentInChildren<weaponSet>();
        int left = DataConfig.leftHand;
        if(left != leftindex)
        {
            leftindex = left;
            weaponSet.DisableLeft();
            weaponSet.EquipLeft(rotPos[leftindex], weapons[leftindex],leftindex);

        }
        int right = DataConfig.rightHand;
        if (right != rightindex)
        {
            rightindex = right;
            weaponSet.DisableRight();
            weaponSet.EquipRight(rotPos[rightindex], weapons[rightindex], rightindex);

        }
        int body = DataConfig.body;

        if (body != bodyindex)
        {
            bodyindex = body;
            weaponSet.DisableBody();
            weaponSet.EquipBody(rotPos[bodyindex], weapons[bodyindex], bodyindex);

        }
        int head = DataConfig.head;
        if (head != headindex)
        {
            headindex = head;
            weaponSet.DisableHead();
            weaponSet.EquipHead(rotPos[headindex], weapons[headindex], headindex);

        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
