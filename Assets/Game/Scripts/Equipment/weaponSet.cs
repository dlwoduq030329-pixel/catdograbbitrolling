using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class weaponSet : MonoBehaviour
{
    [SerializeField]
    Transform left;
    [SerializeField]
    Transform right;
    [SerializeField]
    Transform body;
    [SerializeField]
    Transform head;
    GameObject leftWeapon;
    GameObject rightWeapon;
    GameObject bodyWeapon;
    GameObject headWeapon;
    [SerializeField]
    GameObject nullWeapon;

    Dictionary<int, GameObject> leftPool = new Dictionary<int, GameObject>();
    Dictionary<int, GameObject> rightPool = new Dictionary<int, GameObject>();
    Dictionary<int, GameObject> bodyPool = new Dictionary<int, GameObject>();
    Dictionary<int, GameObject> headPool = new Dictionary<int, GameObject>();


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void EquipLeft(Vector3 rot,GameObject prefab,int index)
    {
        if (leftPool.ContainsKey(index))
        {

            leftPool[index].gameObject.SetActive(true);
            leftWeapon = leftPool[index].gameObject;
        }else
        {
            var temp = Instantiate(prefab, left);
            temp.transform.localRotation = Quaternion.Euler(rot);
            leftPool.Add(index, temp);
            leftWeapon = temp;
        }
        leftWeapon.transform.localRotation = Quaternion.Euler(-90f, 0, 100f);

    }

    public void DisableLeft()
    {
        if (leftWeapon == null) return;
        leftWeapon.SetActive(false);
    }

    public void EquipRight(Vector3 rot, GameObject prefab, int index)
    {
        if (rightPool.ContainsKey(index))
        {
            rightPool[index].gameObject.SetActive(true);
            rightWeapon = rightPool[index].gameObject;

        }
        else
        {
            var temp = Instantiate(prefab, right);
            temp.transform.localRotation = Quaternion.Euler(rot);
            rightPool.Add(index, temp);
            rightWeapon =temp;
        }
        rightWeapon.transform.localRotation = Quaternion.Euler(-90f, 0, 100f);

    }

    public void DisableRight()
    {
        if (rightWeapon == null) return;

        rightWeapon.SetActive(false);
    }

    public void EquipBody(Vector3 rot, GameObject prefab, int index)
    {
        if (bodyPool.ContainsKey(index))
        {
            bodyPool[index].gameObject.SetActive(true);
            bodyWeapon = bodyPool[index].gameObject;

        }
        else
        {
            var temp = Instantiate(prefab, body);
            temp.transform.localRotation = Quaternion.Euler(rot);
            bodyPool.Add(index, temp);
            bodyWeapon = temp;
        }
    }

    public void DisableBody()
    {
        if (bodyWeapon == null) return;

        bodyWeapon.SetActive(false);
    }

    public void EquipHead(Vector3 rot, GameObject prefab, int index)
    {
        if (headPool.ContainsKey(index))
        {
            headPool[index].gameObject.SetActive(true);
            headWeapon = headPool[index].gameObject;

        }
        else
        {
            var temp = Instantiate(prefab, head);
            temp.transform.localRotation = Quaternion.Euler(rot);
            headPool.Add(index, temp);
            headWeapon = temp;
        }
    }

    public void DisableHead()
    {
        if (headWeapon == null) return;

        headWeapon.SetActive(false);
    }

    public void EquipAdapt(int left,int right,int body,int head)
    {
        DisableLeft();
        DisableRight();
        DisableHead();
        DisableBody();

        if (SoundManager.Instance != null) SoundManager.Instance.GetWeapon();

        int equipCount = DataPool.Instance.equipDatabase.equip.Count;
        left = Mathf.Clamp(left, 0, equipCount - 1);
        right = Mathf.Clamp(right, 0, equipCount - 1);
        body = Mathf.Clamp(body, 0, equipCount - 1);
        head = Mathf.Clamp(head, 0, equipCount - 1);

        if (DataPool.Instance.equipDatabase.equip[left].weaponKind == WeaponKind.TwoHand && 
            DataPool.Instance.equipDatabase.equip[right].weaponKind == WeaponKind.TwoHand)
        {
            GameObject temp = DataPool.Instance.equipDatabase.equip[left].weaponPrefab == null
    ? nullWeapon : DataPool.Instance.equipDatabase.equip[left].weaponPrefab;
            EquipLeft(Vector3.zero, temp, left);

            temp = DataPool.Instance.equipDatabase.equip[left].weaponPrefab2 == null
                ? nullWeapon : DataPool.Instance.equipDatabase.equip[left].weaponPrefab2;
            EquipRight(Vector3.zero, temp, right);

            temp = DataPool.Instance.equipDatabase.equip[body].weaponPrefab == null
           ? nullWeapon : DataPool.Instance.equipDatabase.equip[body].weaponPrefab;
            EquipBody(Vector3.zero, temp, body);

            temp = DataPool.Instance.equipDatabase.equip[head].weaponPrefab == null
    ? nullWeapon : DataPool.Instance.equipDatabase.equip[head].weaponPrefab;
            EquipHead(Vector3.zero, temp, head);

        }
        else
        {
            GameObject temp = DataPool.Instance.equipDatabase.equip[left].weaponPrefab == null
    ? nullWeapon : DataPool.Instance.equipDatabase.equip[left].weaponPrefab;
            EquipLeft(Vector3.zero, temp, left);

            temp = DataPool.Instance.equipDatabase.equip[right].weaponPrefab == null
                ? nullWeapon : DataPool.Instance.equipDatabase.equip[right].weaponPrefab;
            EquipRight(Vector3.zero, temp, right);

            temp = DataPool.Instance.equipDatabase.equip[body].weaponPrefab == null
           ? nullWeapon : DataPool.Instance.equipDatabase.equip[body].weaponPrefab;
            EquipBody(Vector3.zero, temp, body);

            temp = DataPool.Instance.equipDatabase.equip[head].weaponPrefab == null
    ? nullWeapon : DataPool.Instance.equipDatabase.equip[head].weaponPrefab;
            EquipHead(Vector3.zero, temp, head);

        }





    }


}
