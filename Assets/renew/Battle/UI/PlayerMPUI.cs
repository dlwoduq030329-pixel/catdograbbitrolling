using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player의 <see cref="BattleUnitMP"/> 변경 이벤트를 받아 현재 MP를 화면에 표시한다.
/// 크리스탈 이미지와 선택적으로 연결한 현재/최대 MP 텍스트를 갱신한다.
/// </summary>
public class PlayerMPUI : MonoBehaviour
{
    [Header("크리스탈형 MP 바")]
    [InspectorName("마나 크리스탈 이미지 목록")]
    [SerializeField, Tooltip("크리스탈 Image를 채워지는 순서대로 연결합니다. 가로형 UI는 왼쪽부터 연결합니다.")]
    private Image[] manaCrystalImages;

    [InspectorName("채워진 크리스탈 스프라이트")]
    [SerializeField]
    private Sprite manaCrystalFilledSprite;

    [InspectorName("빈 크리스탈 스프라이트")]
    [SerializeField]
    private Sprite manaCrystalEmptySprite;

    [SerializeField, InspectorName("현재 최대 MP 텍스트")]
    private TMP_Text manaValueText;

    [SerializeField, InspectorName("빈 스프라이트가 없을 때 소모된 크리스탈 색상")]
    private Color depletedCrystalColor = new Color(0.25f, 0.25f, 0.25f, 1f);

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
        UpdateManaCrystals(currentMana, maximumMana);
        if (manaValueText != null)
        {
            manaValueText.text = $"{currentMana}/{maximumMana}";
        }
    }

    /// <summary>
    /// 크리스탈 목록이 연결돼 있으면 인덱스가 최대 MP 미만인 크리스탈만 활성화하고,
    /// 현재 MP보다 작은 인덱스는 채워진 스프라이트, 그 이상은 빈 스프라이트로 표시한다.
    /// 최대 MP를 넘는 인덱스의 크리스탈은 비활성화해서 20칸짜리 바 중 실제 보유한 칸만 보이게 한다.
    /// </summary>
    private void UpdateManaCrystals(int currentMana, int maximumMana)
    {
        if (manaCrystalImages == null || manaCrystalImages.Length == 0)
        {
            return;
        }

        for (int i = 0; i < manaCrystalImages.Length; i++)
        {
            Image crystal = manaCrystalImages[i];
            if (crystal == null)
            {
                continue;
            }

            bool withinMax = i < maximumMana;
            crystal.gameObject.SetActive(withinMax);
            if (!withinMax)
            {
                continue;
            }

            bool isFilled = i < currentMana;
            Sprite targetSprite = isFilled ? manaCrystalFilledSprite : manaCrystalEmptySprite;
            if (targetSprite != null)
            {
                crystal.sprite = targetSprite;
            }
            // 새 가로형 UI는 빈 이미지 대신 같은 크리스탈을 어둡게 표시한다.
            if (manaCrystalEmptySprite == null)
            {
                crystal.color = isFilled ? Color.white : depletedCrystalColor;
            }
        }
    }
}
