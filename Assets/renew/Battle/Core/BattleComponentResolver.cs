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
        return component != null ? component : owner.AddComponent<T>();
    }
}
