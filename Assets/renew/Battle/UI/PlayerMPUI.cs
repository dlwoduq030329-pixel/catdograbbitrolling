using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Player의 <see cref="BattleUnitMP"/> 변경 이벤트를 받아 현재 MP를 화면에 표시한다.
/// 현재 UI는 세로 실린더형 Filled Image와 선택적 수치 Text를 사용한다.
/// 추후 원형 MP 이미지가 제공되면 데이터 구독은 유지하고 이미지 표현 부분만 교체한다.
/// </summary>
public class PlayerMPUI : MonoBehaviour
{
    [Header("플레이어 행동력 화면 참조")]
    [InspectorName("마나 채움형 이미지")]
    [FormerlySerializedAs("manaFillImage")]
    [SerializeField, Tooltip("현재는 세로 실린더 형태로 MP 비율을 표시하는 Filled Image입니다. 원형 MP UI 제공 후 교체할 대상입니다.")]
    private Image playerManaFillImage;

    [InspectorName("행동력 수치 텍스트(선택 사항)")]
    [FormerlySerializedAs("manaText")]
    [SerializeField, Tooltip("현재 MP와 최대 MP를 '현재 / 최대' 형식으로 표시할 선택적 TMP Text입니다. 연결하지 않으면 이미지만 갱신합니다.")]
    private TMP_Text playerManaValueText;

    // 현재 UI가 관찰 중인 Player MP다. 교체 시 이전 이벤트를 먼저 해제하여 중복 갱신을 막는다.
    private BattleUnitMP observedPlayerMana;

    /// <summary>UI가 파괴된 뒤 Player MP 이벤트가 이 인스턴스를 계속 호출하지 않도록 구독을 해제한다.</summary>
    private void OnDestroy()
    {
        StopObservingPlayerMana();
    }

    /// <summary>
    /// 생성·등록된 Player의 MP 컴포넌트를 전달받아 변경 이벤트를 구독한다.
    /// 같은 대상을 다시 전달받으면 구독은 늘리지 않고 현재 값만 즉시 다시 표시한다.
    /// </summary>
    public void BindPlayerMana(BattleUnitMP playerMana)
    {
        if (observedPlayerMana == playerMana)
        {
            UpdatePlayerManaDisplay(
                observedPlayerMana != null ? observedPlayerMana.CurrentMP : 0,
                observedPlayerMana != null ? observedPlayerMana.MaxMP : 0);
            return;
        }

        StopObservingPlayerMana();
        observedPlayerMana = playerMana;

        if (observedPlayerMana != null)
        {
            observedPlayerMana.MPChanged += UpdatePlayerManaDisplay;
            UpdatePlayerManaDisplay(observedPlayerMana.CurrentMP, observedPlayerMana.MaxMP);
        }
        else
        {
            UpdatePlayerManaDisplay(0, 0);
        }
    }

    /// <summary>이전에 표시하던 Player MP의 변경 이벤트 연결을 제거하고 관찰 대상을 비운다.</summary>
    private void StopObservingPlayerMana()
    {
        if (observedPlayerMana != null)
        {
            observedPlayerMana.MPChanged -= UpdatePlayerManaDisplay;
            observedPlayerMana = null;
        }
    }

    /// <summary>
    /// BattleUnitMP.MPChanged가 전달한 현재·최대 MP를 이미지 비율과 선택적 수치 Text에 반영한다.
    /// 최대 MP가 0인 초기 상태에서는 0으로 나누지 않고 채움 비율을 0으로 표시한다.
    /// </summary>
    private void UpdatePlayerManaDisplay(int currentMana, int maximumMana)
    {
        if (playerManaFillImage != null)
        {
            playerManaFillImage.fillAmount = maximumMana > 0
                ? (float)currentMana / maximumMana
                : 0f;
        }

        if (playerManaValueText != null)
        {
            playerManaValueText.text = $"{currentMana} / {maximumMana}";
        }
    }
}
