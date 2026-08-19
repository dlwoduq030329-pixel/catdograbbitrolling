using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DataConfig
{
    const string RunSaveKey = "TRYTRY_RUN_SAVE_V2";
    public static bool[] isSelected = new bool[4] { false, false, false, false };
    public static int[] playerDatas = new int[4] {100, 100, 100, 100, };
    public static int tribe;
    public static List<int> cardData = new List<int>();
    public static List <int> values = new List<int>();  
    public static Dictionary<int ,int> CardsCount = new Dictionary<int ,int>();//key = CardIndex,Value = CardCount
    public static int nowPos;

    public static string playerName;
    public static int hard;
    public static int count;

    public static int leftHand = 0;
    public static EquipData leftDa;
    public static int rightHand = 0;
    public static EquipData rightDa;
    public static int head = 0;
    public static EquipData headDa;
    public static int body = 0;
    public static EquipData bodyDa;

    public static string PlayerName;

    public static bool isTwoHand = false;

    public static int playerMoney = 180;
    public static int turnCount;

    public static EquipData[] myEqipData = new EquipData[4];
    public static int stage = 1;
    public static int turn = 0;
    public static int selectedTitleIndex = -1;
    public static int selectedJobIndex = -1;

    public static bool isBattled = false;

    public static void InitCard()
    {
        cardData.Clear();
        for(int i =0;i<10;i++)
        {
            cardData.Add(10);
        }
        CardsCount.Clear();
    }

    public static void Init()
    {
        for(int i =0; i< isSelected.Length;i++)
        {
           isSelected[i] = false;
        }

        playerDatas[0] = 1;
        playerDatas[1] = 0;
        playerDatas[2] = 0;
        playerDatas[3] = 0;
        tribe = 0;
        cardData.Clear();
        CardsCount.Clear();
        nowPos = 0;
        playerName = string.Empty;
        leftHand = 0;
        rightHand = 0;
        body = 0;
        head = 0;

        leftDa = null;
        rightDa = null;
        bodyDa = null;
        headDa = null;

        isTwoHand = false;
        stage = 1;
        turn = 0;
        selectedTitleIndex = -1;
        selectedJobIndex = -1;

        playerMoney = 180;
        turnCount = 0;

        for(int k =0; k< myEqipData.Length;k++)
        {
            myEqipData[k] = null;
        }

        values.Clear();
        rot = Quaternion.identity;
    }

    static Quaternion rot;
    public static Quaternion Rot
    {
        get { return rot; }
        set { rot = value; }
    }


    public static void AddCard(int cardIndexinList, int cardindex)
    {
        cardData[cardIndexinList] = cardindex;
        //Debug.Log(cardIndexinList + " 그리고 " + cardindex);
    }

    public static void AddDic(int key,int x)
    {
       // int count = 0;
        if(CardsCount.ContainsKey(key))
        {
            if (x > 0 && CardsCount[key] >= 2)
                return;
            if (x == -1 &&CardsCount[key] +x == 0)
            {
                CardsCount[key] = 0;
                CardsCount.Remove(key);
                return;
            }
            CardsCount[key] += x;
        }else
        {
            if (x > 0)
            {
                CardsCount[key] = x;
               // Debug.Log("새로운 카드 추가!");
            }
        }

     //   Debug.Log(key + "번 카드 : " + x + " 개");
    }

    public static void SaveData(int str,int wis,int dex,int vit,int tri,int pos)
    {
        //cardData.Clear();
        //cardData=new List<int>(data);
        string temp = string.Join(",", cardData);
        PlayerPrefs.SetString("MyDeckList", temp);

        

        string valueString = string.Join(",", values);
        Debug.Log("values : " + valueString);
        PlayerPrefs.SetString("values", valueString);

        playerDatas[0] = str;
        playerDatas[1] = wis;
        playerDatas[2] = dex;
        playerDatas[3] = vit;

        tribe = tri;
        nowPos = pos;
        PlayerPrefs.SetInt("data0", playerDatas[0]);
        PlayerPrefs.SetInt("data1", playerDatas[1]);
        PlayerPrefs.SetInt("data2", playerDatas[2]);
        PlayerPrefs.SetInt("data3", playerDatas[3]);
        PlayerPrefs.SetInt("tribe", tribe);
        PlayerPrefs.SetInt("HasData", 1);
        PlayerPrefs.SetInt("posIndex", nowPos);
        SaveCurrentState();
    }

    public static void LoadData()
    {
        if (TryLoadCurrentState()) return;
        if (!PlayerPrefs.HasKey("HasData")) return;
        playerDatas[0] = PlayerPrefs.GetInt("data0");
        playerDatas[1] = PlayerPrefs.GetInt("data1");
        playerDatas[2] = PlayerPrefs.GetInt("data2");
        playerDatas[3] = PlayerPrefs.GetInt("data3");
        tribe = PlayerPrefs.GetInt("tribe");
        nowPos = PlayerPrefs.GetInt("posIndex");
        string data = PlayerPrefs.GetString("MyDeckList");
        cardData.Clear();
        values.Clear();
        foreach (var s in data.Split(','))
        {
            if (int.TryParse(s, out int cardId)) cardData.Add(cardId);
        }
        string keystring = PlayerPrefs.GetString("keys");


        string valuestring = PlayerPrefs.GetString("values");
        foreach (var s in keystring.Split(','))
        {
            //values.Add(int.Parse(s));
        }
        SaveCurrentState();
    }


    public static void SaveData()
    {
        //cardData.Clear();
        //cardData=new List<int>(data);
        string temp = string.Join(",", cardData);
        PlayerPrefs.SetString("MyDeckList", temp);



        string valueString = string.Join(",", values);
        //Debug.Log("values : " + valueString);
        PlayerPrefs.SetString("values", valueString);

        PlayerPrefs.SetInt("data0", playerDatas[0]);
        PlayerPrefs.SetInt("data1", playerDatas[1]);
        PlayerPrefs.SetInt("data2", playerDatas[2]);
        PlayerPrefs.SetInt("data3", playerDatas[3]);
        PlayerPrefs.SetInt("tribe", tribe);
        PlayerPrefs.SetInt("HasData", 1);
        PlayerPrefs.SetInt("posIndex", nowPos);
        SaveCurrentState();
    }

    public static void SaveCurrentState()
    {
        RunSaveData save = new RunSaveData
        {
            playerStats = (int[])playerDatas.Clone(),
            tribe = tribe,
            deck = new List<int>(cardData),
            boardPosition = nowPos,
            playerName = playerName,
            difficulty = hard,
            enemyCount = count,
            gold = playerMoney,
            turnCount = turnCount,
            stage = stage,
            turn = turn,
            titleIndex = selectedTitleIndex,
            jobIndex = selectedJobIndex,
            boardRotation = rot,
            left = EquipSaveData.FromEquip(leftDa),
            right = EquipSaveData.FromEquip(rightDa),
            head = EquipSaveData.FromEquip(headDa),
            body = EquipSaveData.FromEquip(bodyDa)
        };

        foreach (KeyValuePair<int, int> pair in CardsCount)
        {
            save.cardIds.Add(pair.Key);
            save.cardCounts.Add(pair.Value);
        }

        PlayerPrefs.SetString(RunSaveKey, JsonUtility.ToJson(save));
        PlayerPrefs.SetInt("HasData", 1);
        PlayerPrefs.Save();
    }

    static bool TryLoadCurrentState()
    {
        if (!PlayerPrefs.HasKey(RunSaveKey)) return false;
        try
        {
            RunSaveData save = JsonUtility.FromJson<RunSaveData>(PlayerPrefs.GetString(RunSaveKey));
            if (save == null || save.saveVersion != 2 || save.playerStats == null || save.playerStats.Length != 4)
            {
                Debug.LogWarning("지원하지 않거나 손상된 저장 데이터입니다. 구버전 저장을 시도합니다.");
                return false;
            }

            playerDatas = (int[])save.playerStats.Clone();
            tribe = save.tribe;
            cardData = save.deck ?? new List<int>();
            CardsCount.Clear();
            int pairCount = Mathf.Min(save.cardIds?.Count ?? 0, save.cardCounts?.Count ?? 0);
            for (int i = 0; i < pairCount; i++)
            {
                if (save.cardCounts[i] > 0) CardsCount[save.cardIds[i]] = save.cardCounts[i];
            }
            nowPos = save.boardPosition;
            playerName = save.playerName ?? string.Empty;
            hard = save.difficulty;
            count = save.enemyCount;
            playerMoney = save.gold;
            turnCount = save.turnCount;
            stage = Mathf.Max(1, save.stage);
            turn = save.turn;
            selectedTitleIndex = save.titleIndex;
            selectedJobIndex = save.jobIndex;
            rot = save.boardRotation;
            leftDa = RestoreEquip(save.left);
            rightDa = save.right != null && save.left != null && save.right.index == save.left.index && save.right.rarity == save.left.rarity
                ? leftDa
                : RestoreEquip(save.right);
            headDa = RestoreEquip(save.head);
            bodyDa = RestoreEquip(save.body);
            leftHand = leftDa?.weaponIndex ?? 0;
            rightHand = rightDa?.weaponIndex ?? 0;
            head = headDa?.weaponIndex ?? 0;
            body = bodyDa?.weaponIndex ?? 0;
            isTwoHand = leftDa != null && ReferenceEquals(leftDa, rightDa) && leftDa.weaponKind == WeaponKind.TwoHand;
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"저장 데이터 로드 실패: {exception.Message}");
            return false;
        }
    }

    static EquipData RestoreEquip(EquipSaveData saved)
    {
        if (saved == null || DataPool.Instance == null || DataPool.Instance.equipDatabase == null) return null;
        if (saved.index < 0 || saved.index >= DataPool.Instance.equipDatabase.equip.Count) return null;
        EquipData equip = DataPool.Instance.equipDatabase.equip[saved.index].Clone();
        equip.weapon = (weaponSt)saved.rarity;
        equip.stroffset = saved.str;
        equip.wisoffset = saved.wis;
        equip.dexoffset = saved.dex;
        equip.vitoffset = saved.vit;
        equip.cost = saved.cost;
        return equip;
    }

    public static void GetWeapon(EquipData myEqip)
    {
        switch(myEqip.weaponKind)
        {
            case WeaponKind.Hand:
                {

                    if(leftDa == null)
                    {
                        Debug.Log("왼손에 장착");
                        leftDa = myEqip;
                        leftHand = myEqip.weaponIndex;
                        playerDatas[0] += leftDa.stroffset;
                        playerDatas[1] += leftDa.wisoffset;
                        playerDatas[2] += leftDa.dexoffset;
                        playerDatas[3] += leftDa.vitoffset;
                    }
                    else if(rightDa == null)
                    {
                        Debug.Log("오른손에 장착");

                        rightDa = myEqip;
                        rightHand = myEqip.weaponIndex;
                        playerDatas[0] += rightDa.stroffset;
                        playerDatas[1] += rightDa.wisoffset;
                        playerDatas[2] += rightDa.dexoffset;
                        playerDatas[3] += rightDa.vitoffset;

                    }else
                    {
                        SellLeftWeapon();
                        Debug.Log("왼손에 장착");
                        leftDa = myEqip;
                        leftHand = myEqip.weaponIndex;
                        playerDatas[0] += leftDa.stroffset;
                        playerDatas[1] += leftDa.wisoffset;
                        playerDatas[2] += leftDa.dexoffset;
                        playerDatas[3] += leftDa.vitoffset;
                        Debug.Log("빈 공간 없음");
                    }
                }
                break;
            case WeaponKind.Body:
                {
                    SellBodyWeapon();

                    bodyDa = myEqip;
                    playerDatas[0] += bodyDa.stroffset;
                    playerDatas[1] += bodyDa.wisoffset;
                    playerDatas[2] += bodyDa.dexoffset;
                    playerDatas[3] += bodyDa.vitoffset;

                    body = myEqip.weaponIndex;
                }
                break;
            case WeaponKind.Head:
                {
                    SellheadWeapon();

                    headDa = myEqip;
                    playerDatas[0] += headDa.stroffset;
                    playerDatas[1] += headDa.wisoffset;
                    playerDatas[2] += headDa.dexoffset;
                    playerDatas[3] += headDa.vitoffset;

                    head = myEqip.weaponIndex;

                }
                break;
            case WeaponKind.TwoHand:
                {
                    SellLeftWeapon();
                    SellRightWeapon();

                    leftDa = myEqip;
                    rightDa = myEqip;

                    playerDatas[0] += leftDa.stroffset;
                    playerDatas[1] += leftDa.wisoffset;
                    playerDatas[2] += leftDa.dexoffset;
                    playerDatas[3] += leftDa.vitoffset;

                    leftHand = myEqip.weaponIndex;
                    rightHand = myEqip.weaponIndex;
                    isTwoHand = true;
                }
                break;
        }
    }

    public static int returnEmpty()
    {
        int temp = 0;

        if(leftHand == 0)
        {
            temp = 0;
        }else if(rightHand == 0)
        {
            temp = 1;
        }
        return temp;
    }

    public static bool isTwoHandEquip()
    {
        bool temp = false;

        if (leftDa != null&& rightDa != null)
        {
            temp = true;
        }else
        {
            temp=false;
        }

        return temp;
    }

    public static void EquipHandInSlot(EquipData equipment, bool equipLeft)
    {
        if (equipment == null || equipment.weaponKind != WeaponKind.Hand) return;

        if (isTwoHand)
            SellTwoHandWeapon();
        else if (equipLeft)
            SellLeftWeapon();
        else
            SellRightWeapon();

        if (equipLeft)
        {
            leftDa = equipment;
            leftHand = equipment.weaponIndex;
        }
        else
        {
            rightDa = equipment;
            rightHand = equipment.weaponIndex;
        }

        playerDatas[0] += equipment.stroffset;
        playerDatas[1] += equipment.wisoffset;
        playerDatas[2] += equipment.dexoffset;
        playerDatas[3] += equipment.vitoffset;
        isTwoHand = false;
    }

    public static EquipData GetActiveHandWeapon(playerAttackST attackType)
    {
        if (leftDa == null) return rightDa;
        if (rightDa == null) return leftDa;
        if (ReferenceEquals(leftDa, rightDa)) return leftDa;

        if (attackType == playerAttackST.Range)
        {
            return leftDa.attackRange >= rightDa.attackRange ? leftDa : rightDa;
        }

        return leftDa.attackRange <= rightDa.attackRange ? leftDa : rightDa;
    }

    public static int GetCombatStat(int statIndex, playerAttackST attackType)
    {
        if (statIndex < 0 || statIndex >= playerDatas.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(statIndex));
        }

        int value = playerDatas[statIndex];
        value -= GetEquipOffset(leftDa, statIndex);
        if (rightDa != null && !ReferenceEquals(leftDa, rightDa))
        {
            value -= GetEquipOffset(rightDa, statIndex);
        }

        EquipData activeWeapon = GetActiveHandWeapon(attackType);
        value += GetEquipOffset(activeWeapon, statIndex);
        return Mathf.Max(0, value);
    }

    static int GetEquipOffset(EquipData equip, int statIndex)
    {
        if (equip == null) return 0;
        switch (statIndex)
        {
            case 0: return equip.stroffset;
            case 1: return equip.wisoffset;
            case 2: return equip.dexoffset;
            case 3: return equip.vitoffset;
            default: return 0;
        }
    }

    public static void SellLeftWeapon()
    {
        if (leftDa == null)
        {
            Debug.Log("정보 없음");
            return;
        }

        if (isTwoHand)
        {
            SellTwoHandWeapon();
            return;
        }
        int tempCost = GetSaleValue(leftDa.cost);
        playerDatas[0] -= leftDa.stroffset;
        playerDatas[1] -= leftDa.wisoffset;
        playerDatas[2] -= leftDa.dexoffset;
        playerDatas[3] -= leftDa.vitoffset;
        AddEquipmentSaleGold(tempCost);
        leftDa = null;
        leftHand = 0;
    }
    public static void SellRightWeapon()
    {
        if (rightDa == null)
        {
            Debug.Log("정보 없음");
            return;
        }

        if (isTwoHand)
        {
            SellTwoHandWeapon();
            return;
        }
        playerDatas[0] -= rightDa.stroffset;
        playerDatas[1] -= rightDa.wisoffset;
        playerDatas[2] -= rightDa.dexoffset;
        playerDatas[3] -= rightDa.vitoffset;

        int tempCost = GetSaleValue(rightDa.cost);
        AddEquipmentSaleGold(tempCost);
        rightDa = null;
        rightHand = 0;
    }
    public static void SellheadWeapon()
    {
        if (headDa == null)
        {
            Debug.Log("정보 없음");
            return;
        }

        playerDatas[0] -= headDa.stroffset;
        playerDatas[1] -= headDa.wisoffset;
        playerDatas[2] -= headDa.dexoffset;
        playerDatas[3] -= headDa.vitoffset;

        int tempCost = GetSaleValue(headDa.cost);
        AddEquipmentSaleGold(tempCost);
        headDa = null;
        head = 0;
    }
    public static void SellBodyWeapon()
    {
        if (bodyDa == null)
        {
            Debug.Log("정보 없음");
            return;
        }

        playerDatas[0] -= bodyDa.stroffset;
        playerDatas[1] -= bodyDa.wisoffset;
        playerDatas[2] -= bodyDa.dexoffset;
        playerDatas[3] -= bodyDa.vitoffset;

        int tempCost = GetSaleValue(bodyDa.cost);
        AddEquipmentSaleGold(tempCost);
        bodyDa = null;
        body = 0;
    }

    public static void SellTwoHandWeapon()
    {
        playerDatas[0] -= leftDa.stroffset;
        playerDatas[1] -= leftDa.wisoffset;
        playerDatas[2] -= leftDa.dexoffset;
        playerDatas[3] -= leftDa.vitoffset;
        int tempCost = GetSaleValue(leftDa.cost);
        AddEquipmentSaleGold(tempCost);

        isTwoHand = false;
        leftHand = 0;
        rightHand = 0;
        leftDa = null;
        rightDa = null;


    }

    public static int GetSaleValue(int purchasePrice)
    {
        return Mathf.Max(0, Mathf.FloorToInt(purchasePrice * 0.5f));
    }

    static void AddEquipmentSaleGold(int amount)
    {
        if (GameManagerInMain.Instance != null)
            GameManagerInMain.Instance.SetGold(amount);
        else
            playerMoney += amount;
    }
}
