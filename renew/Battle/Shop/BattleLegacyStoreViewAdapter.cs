using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 원본 Store 프리팹(Assets/Game/UI/UITexture/Store/Store.prefab)의 판매 슬롯과
/// 표시 요소를 Battle 상점에 연결한다.
///
/// 이 프리팹에는 StoreManager/StoreCardOwn 같은 레거시 상점 스크립트가 붙어있지
/// 않다 (직접 확인: 프리팹 전체에서 두 스크립트의 GUID가 한 번도 등장하지 않는다).
/// "StoreItems" 스크롤 목록 아래 있는 "ShopItem01 (1)/(2)/(3)" 오브젝트는 로직 없는
/// 순수 UI 프리팹 인스턴스(Item01 하위에 CardImage/CardName/Price/Button)일 뿐이다.
/// 그래서 리플렉션으로 레거시 스크립트 필드를 읽던 예전 방식 대신, 이름으로 직접
/// 찾아 바인딩한다. 이전 방식은 StoreManager를 못 찾아 항상 실패했고, 그 결과
/// Battle 상점이 매번 레거시 UI 대신 임시 회색 박스 UI로 폴백하고 있었다.
///
/// 참고: 같은 프리팹 안에 "Inventory" 섹션도 동일한 이름("ShopItem01 (1)" 등)의
/// 오브젝트를 재사용하므로, 반드시 "StoreItems" 하위에서만 검색해야 한다.
/// </summary>
public static class BattleLegacyStoreViewAdapter
{
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

            Transform item = FindNamedChild(slotRoot, "Item01");
            if (item == null) return false;

            Image cardImage = legacySlots[i].StoreImage;
            TMP_Text cardName = legacySlots[i].StoreNameText;
            TMP_Text price = legacySlots[i].StorePriceText;
            Button button = FindNamedChild(item, "Button")?.GetComponent<Button>();
            if (cardImage == null || cardName == null || price == null || button == null) return false;

            result.SlotRoots[i] = slotRoot.gameObject;
            result.Buttons[i] = button;
            result.CardImages[i] = cardImage;
            result.CardNames[i] = cardName;
            result.CardPrices[i] = price;
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

        // "EscButton" still carries its original prefab-authored persistent onClick
        // (a direct GameObject.SetActive(false) on the Event_Store root). That call
        // bypasses BattleCardShopSystem.Close() entirely, so it never restores the
        // battle HUD or releases the modal input lock. The caller must replace this
        // button's onClick with its own Close() before use.
        Transform escTransform = FindNamedChild(legacyRoot.transform, "EscButton");
        result.EscButton = escTransform != null ? escTransform.GetComponent<Button>() : null;

        binding = result;
        return true;
    }

    private static Transform FindNamedChild(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != root && child.gameObject.name == objectName) return child;
        return null;
    }
}
