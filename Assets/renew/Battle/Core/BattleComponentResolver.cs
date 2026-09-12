using UnityEngine;

/// <summary>
/// 전투 조정기가 사용하는 전용 컴포넌트를 기존 참조, 같은 오브젝트 검색, 자동 부착 순서로 반환한다.
/// 컴포넌트 설정과 게임 규칙은 담당하지 않는다.
/// </summary>
public static class BattleComponentResolver
{
    /// <summary>현재 참조를 우선 사용하고 없으면 소유 오브젝트에서 찾거나 새로 부착한다.</summary>
    public static T GetOrAdd<T>(GameObject owner, T current) where T : Component
    {
        if (current != null)
        {
            return current;
        }

        if (owner == null)
        {
            return null;
        }

        T component = owner.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        // 동적 부착 추적용 로그. Scene/Prefab에 이 컴포넌트가 없어서 런타임에 새로 붙였다는 뜻이다.
        Debug.LogWarning($"[동적 부착] {owner.name}에 {typeof(T).Name}이 없어서 런타임에 자동으로 붙였습니다.", owner);
        return owner.AddComponent<T>();
    }
}
