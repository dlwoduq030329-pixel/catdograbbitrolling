#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>상점 View를 임시 부모에서 실제 Event_Store 루트로 한 번 이전하는 편집기 도구다.</summary>
[InitializeOnLoad]
internal static class MoveBattleShopViewToEventStoreOnce
{
    private const string TargetSceneName = "moon_branch Jeon Yong";
    private const string CompletionKey = "MoveBattleShopViewToEventStoreOnce.Completed.v4";

    static MoveBattleShopViewToEventStoreOnce()
    {
        EditorApplication.delayCall += MoveView;
    }

    private static void MoveView()
    {
        if (SessionState.GetBool(CompletionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != TargetSceneName) return;

        BattleShopView oldView = Object.FindFirstObjectByType<BattleShopView>(FindObjectsInactive.Include);
        BattleCardShopSystem shopSystem = Object.FindFirstObjectByType<BattleCardShopSystem>(FindObjectsInactive.Include);
        if (oldView == null || shopSystem == null) return;

        Transform eventStore = oldView.transform.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate != oldView.transform && candidate.name == "Event_Store");
        if (eventStore == null)
        {
            // 이미 Event_Store에 붙어 있다면 참조만 확정하고 끝낸다.
            if (oldView.gameObject.name != "Event_Store") return;
            ResetEventStoreRectTransform(oldView.transform as RectTransform);
            ConfigureEquipmentSlotViews(oldView);
            DisableLegacyStoreBehaviours(oldView.gameObject);
            AssignShopView(shopSystem, oldView);
            Complete(scene);
            return;
        }

        BattleShopView directView = eventStore.GetComponent<BattleShopView>();
        if (directView == null) directView = Undo.AddComponent<BattleShopView>(eventStore.gameObject);
        EditorUtility.CopySerialized(oldView, directView);
        EditorUtility.SetDirty(directView);
        ConfigureEquipmentSlotViews(directView);
        AssignShopView(shopSystem, directView);

        Transform obsoleteWrapper = oldView.transform;
        Transform intendedParent = obsoleteWrapper.parent;
        Undo.SetTransformParent(eventStore, intendedParent, "Event_Store를 Battle Canvas로 이동");
        ResetEventStoreRectTransform(eventStore as RectTransform);
        DisableLegacyStoreBehaviours(eventStore.gameObject);
        Undo.DestroyObjectImmediate(obsoleteWrapper.gameObject);
        Complete(scene);
    }

    /// <summary>기존 EquipStore 4칸의 부위와 Image 참조를 새 표시 전용 View로 이전한다.</summary>
    private static void ConfigureEquipmentSlotViews(BattleShopView shopView)
    {
        EquipStore[] legacySlots = shopView.GetComponentsInChildren<EquipStore>(true);
        BattleShopOwnedEquipmentSlotView[] newSlots =
            new BattleShopOwnedEquipmentSlotView[legacySlots.Length];

        for (int index = 0; index < legacySlots.Length; index++)
        {
            EquipStore legacySlot = legacySlots[index];
            BattleShopOwnedEquipmentSlotView newSlot =
                legacySlot.GetComponent<BattleShopOwnedEquipmentSlotView>();
            if (newSlot == null)
                newSlot = Undo.AddComponent<BattleShopOwnedEquipmentSlotView>(legacySlot.gameObject);

            SerializedObject legacyObject = new SerializedObject(legacySlot);
            SerializedObject newObject = new SerializedObject(newSlot);
            EquipState legacyState = (EquipState)legacyObject.FindProperty("state").enumValueIndex;
            newObject.FindProperty("slotType").enumValueIndex = ConvertSlotType(legacyState);
            newObject.FindProperty("equipmentImage").objectReferenceValue =
                legacyObject.FindProperty("thisIMG").objectReferenceValue;
            newObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(newSlot);
            newSlots[index] = newSlot;
        }

        SerializedObject viewObject = new SerializedObject(shopView);
        SerializedProperty slotsProperty = viewObject.FindProperty("ownedEquipmentSlots");
        slotsProperty.arraySize = newSlots.Length;
        for (int index = 0; index < newSlots.Length; index++)
            slotsProperty.GetArrayElementAtIndex(index).objectReferenceValue = newSlots[index];
        viewObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shopView);
    }

    private static int ConvertSlotType(EquipState legacyState)
    {
        switch (legacyState)
        {
            case EquipState.LeftHand: return (int)PlayerEquipmentSlotType.LeftArm;
            case EquipState.RightHand: return (int)PlayerEquipmentSlotType.RightArm;
            case EquipState.Head: return (int)PlayerEquipmentSlotType.Head;
            case EquipState.Body: return (int)PlayerEquipmentSlotType.Body;
            default: return (int)PlayerEquipmentSlotType.LeftArm;
        }
    }

    /// <summary>
    /// 레거시 OnEnable/OnDisable이 DataPool과 DataConfig를 먼저 만져 예외를 내지 않도록
    /// 실행 전에 비활성 상태를 Scene에 저장한다. 화면 참조는 새 View들이 이미 직접 보관한다.
    /// </summary>
    private static void DisableLegacyStoreBehaviours(GameObject eventStoreRoot)
    {
        Behaviour[] legacyBehaviours = eventStoreRoot.GetComponentsInChildren<Behaviour>(true)
            .Where(component =>
                component is StoreManager ||
                component is StoreSet ||
                component is StoreCardOwn ||
                component is InventoryStore ||
                component is EquipStore ||
                component is sellCard)
            .ToArray();

        foreach (Behaviour legacyBehaviour in legacyBehaviours)
        {
            SerializedObject serializedBehaviour = new SerializedObject(legacyBehaviour);
            SerializedProperty enabledProperty = serializedBehaviour.FindProperty("m_Enabled");
            if (enabledProperty == null) continue;
            enabledProperty.boolValue = false;
            serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(legacyBehaviour);
        }
    }

    /// <summary>
    /// 크기 0인 임시 부모에서 Canvas로 이동할 때 월드 좌표 유지 계산으로 로컬 스케일까지 0이 되는 것을 복구한다.
    /// Event_Store는 Canvas 전체를 덮는 패널이므로 Stretch 기준으로 되돌린다.
    /// </summary>
    private static void ResetEventStoreRectTransform(RectTransform eventStoreRect)
    {
        if (eventStoreRect == null) return;
        Undo.RecordObject(eventStoreRect, "Event_Store 화면 좌표 복구");
        eventStoreRect.localScale = Vector3.one;
        eventStoreRect.localRotation = Quaternion.identity;
        eventStoreRect.anchorMin = Vector2.zero;
        eventStoreRect.anchorMax = Vector2.one;
        eventStoreRect.anchoredPosition = Vector2.zero;
        eventStoreRect.sizeDelta = Vector2.zero;
        EditorUtility.SetDirty(eventStoreRect);
    }

    private static void AssignShopView(BattleCardShopSystem shopSystem, BattleShopView shopView)
    {
        SerializedObject systemObject = new SerializedObject(shopSystem);
        systemObject.FindProperty("shopView").objectReferenceValue = shopView;
        systemObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shopSystem);
    }

    private static void Complete(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SessionState.SetBool(CompletionKey, true);
        Debug.Log("[Shop View Migration] BattleShopView를 Event_Store 루트로 이전했습니다.");
    }
}
#endif
