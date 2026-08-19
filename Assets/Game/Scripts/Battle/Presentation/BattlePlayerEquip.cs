using System.Collections.Generic;
using UnityEngine;

public class BattlePlayerEquip : MonoBehaviour
{
    [SerializeField]
    Transform leftEquip;
    [SerializeField]
    Transform rightEquip;
    [SerializeField]
    Transform headEquip;
    [SerializeField]
    Transform bodyEquip;
    // Start is called before the first frame update
    void Start()
    {

    }

    // SetEquip()이 슬롯 아래 자식을 지우지 않고 매번 새로 Instantiate만 해서, 상점에서
    // 장비를 구매/판매할 때마다 다시 호출하면 예전 무기·방어구 모델이 겹쳐서 계속 쌓였다
    // (원래는 전투 시작 시 딱 한 번만 호출하도록 설계된 것으로 보인다). 슬롯을 비우고
    // 다시 채우도록 바꿔서 장비가 바뀔 때마다 안전하게 반복 호출할 수 있게 한다.
    public void SetEquip()
    {
        if (DataPool.Instance == null || DataPool.Instance.equipDatabase == null ||
            DataPool.Instance.equipDatabase.equip == null)
        {
            Debug.LogError("[BattlePlayerEquip] equipDatabase가 비어 있어 모델을 갱신하지 못했습니다.", this);
            return;
        }

        List<EquipData> equip = DataPool.Instance.equipDatabase.equip;
        int count = equip.Count;

        ApplySlot("Left", leftEquip, equip, count, DataConfig.leftHand, true);
        ApplySlot("Right", rightEquip, equip, count, DataConfig.rightHand, true);
        ApplySlot("Head", headEquip, equip, count, DataConfig.head, false);
        ApplySlot("Body", bodyEquip, equip, count, DataConfig.body, false);
    }

    private void ApplySlot(string slotLabel, Transform slot, List<EquipData> equip, int count, int index, bool isHandSlot)
    {
        if (slot == null)
        {
            Debug.LogWarning($"[BattlePlayerEquip] {slotLabel} 슬롯 Transform이 Inspector에 연결되어 있지 않습니다.", this);
            return;
        }

        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            Destroy(slot.GetChild(i).gameObject);
        }

        if (index <= 0 || index >= count)
        {
            Debug.Log($"[BattlePlayerEquip] {slotLabel} 슬롯: index={index} (미장착 또는 범위 밖, count={count})", this);
            return; // 0번 = 미장착
        }
        GameObject prefab = equip[index].weaponPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[BattlePlayerEquip] {slotLabel} 슬롯: equip[{index}]({equip[index].cardname})의 weaponPrefab이 비어 있습니다.", this);
            return;
        }

        GameObject instance = Instantiate(prefab, slot);
        Debug.Log($"[BattlePlayerEquip] {slotLabel} 슬롯: equip[{index}]({equip[index].cardname}) 모델 생성 완료.", this);
        if (isHandSlot)
        {
            instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 100f);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
