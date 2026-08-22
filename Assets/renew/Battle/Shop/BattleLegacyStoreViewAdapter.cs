using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle 상점이 실제로 바인딩하는 대상은 <c>BattleLegacyStorePrefabReference</c>가 가리키는
/// <c>Assets/renew/Battle/Event_Store.prefab</c>이다(2026-08-22 정리: 아래 문단은 원래
/// <c>Assets/Game/UI/UITexture/Store/Store.prefab</c>을 직접 조사하고 쓴 것이라, 실제 바인딩
/// 대상과 다른 프리팹을 설명하고 있었다 — StoreManager는 두 프리팹 모두에 없지만, StoreCardOwn은
/// <c>Event_Store.prefab</c>의 슬롯 6곳에는 실제로 붙어 있다(직접 재확인:
/// StoreCardOwn.cs.meta guid가 Event_Store.prefab 안에 6회 등장). 아래 CardImage/CardName/Price
/// 필드 접근이 실제로 StoreCardOwn 컴포넌트를 통해 동작하는 이유다.
///
/// "StoreItems" 스크롤 목록 아래 있는 "ShopItem01 (1)/(2)/(3)" 오브젝트 각각에 StoreCardOwn이
/// 붙어 있고, 그 컴포넌트의 StoreImage/StoreNameText/StorePriceText/StorePreviewImage 필드로
/// 이미지·텍스트를 가져온다. Button만 StoreCardOwn에 없어서 "Item01" 자식 아래 이름으로 직접 찾는다.
///
/// 참고: 같은 프리팹 안에 "Inventory" 섹션도 동일한 이름("ShopItem01 (1)" 등)의
/// 오브젝트를 재사용하므로, 반드시 "StoreItems" 하위에서만 검색해야 한다.
/// </summary>
public static class BattleLegacyStoreViewAdapter
{
    /// <summary>
    /// <see cref="TryBind"/>가 레거시 상점 프리팹에서 이름으로 찾아낸 살아있는 씬 오브젝트 참조 묶음이다.
    /// 프리팹 원본이나 설정값이 아니라 "지금 이 상점 세션에서 실제로 화면에 붙어 있는 슬롯 버튼/이미지/
    /// 텍스트가 바로 이것"이라는 확정된 연결 결과를 담는다 — 호출부(BattleCardShopSystem)는 이 결과만
    /// 가지고 상점 UI를 갱신하면 되고, 이름 검색이나 컴포넌트 탐색을 다시 할 필요가 없다.
    /// </summary>
    public sealed class Binding
    {
        public GameObject[] SlotRoots;
        public Button[] Buttons;
        public Image[] CardImages;
        public TMP_Text[] CardNames;
        public TMP_Text[] CardPrices;
        public Image PreviewImage;
        public TMP_Text GoldText;
        public TMP_Text RerollText;
        public Button RerollButton;
        public Button EscButton;
    }

    /// <summary>
    /// 이미 Instantiate된 레거시 상점 프리팹 루트(<paramref name="legacyRoot"/>)에서 이름으로 자식을
    /// 찾아 <paramref name="slotCount"/>개 슬롯 전부와 나머지 UI 요소(골드 텍스트, 리롤, 닫기 버튼)를
    /// 한 번에 연결한다. 슬롯 하나라도 필요한 요소(StoreCardOwn, Item01, Button 등)를 못 찾으면 그
    /// 즉시 실패로 처리한다 — 일부만 연결된 상태로 값을 돌려주지 않는다("전부 성공 아니면 전부 실패").
    /// </summary>
    public static bool TryBind(GameObject legacyRoot, int slotCount, out Binding binding)
    {
        binding = null;
        if (legacyRoot == null) return false;

        Transform storeItems = FindNamedChild(legacyRoot.transform, "StoreItems");
        if (storeItems == null) return false;

        Binding result = new Binding
        {
            SlotRoots = new GameObject[slotCount],
            Buttons = new Button[slotCount],
            CardImages = new Image[slotCount],
            CardNames = new TMP_Text[slotCount],
            CardPrices = new TMP_Text[slotCount],
        };

        StoreCardOwn[] legacySlots = storeItems.GetComponentsInChildren<StoreCardOwn>(true);
        if (legacySlots.Length < slotCount) return false;

        for (int i = 0; i < slotCount; i++)
        {
            // 실제 프리팹의 슬롯 이름은 "ShopItem01 (1)" 처럼 1부터 시작한다.
            Transform slotRoot = legacySlots[i].transform;

            // "item"은 오브젝트 자체가 아니라 그 아래 Button을 찾기 위한 Transform이라 itemTransform으로 명명.
            Transform itemTransform = FindNamedChild(slotRoot, "Item01");
            if (itemTransform == null) return false;

            Image cardImage = legacySlots[i].StoreImage;
            TMP_Text cardName = legacySlots[i].StoreNameText;
            TMP_Text cardPriceText = legacySlots[i].StorePriceText;
            Button button = FindNamedChild(itemTransform, "Button")?.GetComponent<Button>();
            if (cardImage == null || cardName == null || cardPriceText == null || button == null) return false;

            result.SlotRoots[i] = slotRoot.gameObject;
            result.Buttons[i] = button;
            result.CardImages[i] = cardImage;
            result.CardNames[i] = cardName;
            result.CardPrices[i] = cardPriceText;
        }

        result.PreviewImage = legacySlots[0].StorePreviewImage;

        Transform goldTextTransform = FindNamedChild(legacyRoot.transform, "CurrentGoldText");
        result.GoldText = goldTextTransform != null ? goldTextTransform.GetComponent<TMP_Text>() : null;

        // RerollButton 안의 가격 텍스트도 이름이 "Price"라 ShopItem01 슬롯과
        // 겹친다. 반드시 RerollButton 하위에서만 찾는다.
        Transform rerollTransform = FindNamedChild(legacyRoot.transform, "RerollButton");
        if (rerollTransform != null)
        {
            result.RerollButton = rerollTransform.GetComponent<Button>();
            result.RerollText = rerollTransform.GetComponentInChildren<TMP_Text>(true);
        }

        // "EscButton"은 프리팹 제작 당시부터 박혀 있던 영구(persistent) onClick을 여전히 그대로 들고 있다
        // (Event_Store 루트를 직접 SetActive(false)만 하는 이벤트). 이 이벤트는 BattleCardShopSystem.Close()를
        // 전혀 거치지 않으므로, 전투 HUD 복구도 입력 잠금 해제도 일어나지 않는다. 이 버튼을 실제로 쓰기 전에
        // 호출부가 반드시 onClick을 자신의 Close()로 교체해야 한다(BattleCardShopSystem이 이렇게 하고 있음).
        Transform escTransform = FindNamedChild(legacyRoot.transform, "EscButton");
        result.EscButton = escTransform != null ? escTransform.GetComponent<Button>() : null;

        binding = result;
        return true;
    }

    /// <summary>root의 모든 자손(자기 자신 제외)을 재귀로 뒤져 이름이 일치하는 첫 Transform을 반환한다.</summary>
    private static Transform FindNamedChild(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != root && child.gameObject.name == objectName) return child;
        return null;
    }
}
