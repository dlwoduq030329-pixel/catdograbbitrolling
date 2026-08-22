using UnityEngine;

/// <summary>
/// 적 상세 정보 UI를 다시 연결할 때 사용할 자리만 보존한 비활성 컴포넌트다.
/// 이전 구현은 Q+좌클릭 입력 감지, Raycast, Canvas·Panel·TMP Text 런타임 생성을
/// 한 클래스에서 모두 수행해 Inspector에서 UI 구조와 실행 흐름을 추적할 수 없었으므로 제거했다.
/// 추후 직접 제작한 적 정보 프리팹과 명시적 참조 구조가 준비되면 표시 전용 View로 다시 구현한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyInspectView : MonoBehaviour
{
}
