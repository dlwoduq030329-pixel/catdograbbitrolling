using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player의 <see cref="BattleUnitAP"/> 변경 이벤트를 받아 현재 AP를 화면에 표시한다.
/// PlayerMPUI와 완전히 같은 형식(크리스탈 이미지 + 선택적 현재/최대 텍스트)이다.
/// 2026-09-10: 이동 전용 AP가 MP에서 분리되면서, MP UI(PlayerMPUI/MpSystem)와 나란히 붙일
/// 표시 컴포넌트가 필요해져서 그 구조를 그대로 복제해 만들었다.
/// </summary>
public class PlayerAPUI : MonoBehaviour
{
    [Header("크리스탈형 AP 바")]
    [InspectorName("AP 크리스탈 이미지 목록")]
    [SerializeField, Tooltip("크리스탈 Image를 채워지는 순서대로 연결합니다. 가로형 UI는 왼쪽부터 연결합니다.")]
    private Image[] apCrystalImages;

    [InspectorName("채워진 크리스탈 스프라이트")]
    [SerializeField]
    private Sprite apCrystalFilledSprite;

    [InspectorName("빈 크리스탈 스프라이트")]
    [SerializeField]
    private Sprite apCrystalEmptySprite;

    [SerializeField, InspectorName("현재 최대 AP 텍스트")]
    private TMP_Text apValueText;

    [SerializeField, InspectorName("빈 스프라이트가 없을 때 소모된 크리스탈 색상")]
    private Color depletedCrystalColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    // 현재 UI가 관찰 중인 Player AP다. 교체 시 이전 이벤트를 먼저 해제하여 중복 갱신을 막는다.
    private BattleUnitAP observedPlayerAP;

    /// <summary>UI가 파괴된 뒤 Player AP 이벤트가 이 인스턴스를 계속 호출하지 않도록 구독을 해제한다.</summary>
    private void OnDestroy()
    {
        StopObservingPlayerAP();
    }

    /// <summary>
    /// 생성·등록된 Player의 AP 컴포넌트를 전달받아 변경 이벤트를 구독한다.
    /// 같은 대상을 다시 전달받으면 구독은 늘리지 않고 현재 값만 즉시 다시 표시한다.
    /// </summary>
    public void BindPlayerAP(BattleUnitAP playerAP)
    {
        if (observedPlayerAP == playerAP)
        {
            UpdatePlayerApDisplay(
                observedPlayerAP != null ? observedPlayerAP.CurrentAP : 0,
                observedPlayerAP != null ? observedPlayerAP.MaxAP : 0);
            return;
        }

        StopObservingPlayerAP();
        observedPlayerAP = playerAP;

        if (observedPlayerAP != null)
        {
            observedPlayerAP.APChanged += UpdatePlayerApDisplay;
            UpdatePlayerApDisplay(observedPlayerAP.CurrentAP, observedPlayerAP.MaxAP);
        }
        else
        {
            UpdatePlayerApDisplay(0, 0);
        }
    }

    /// <summary>이전에 표시하던 Player AP의 변경 이벤트 연결을 제거하고 관찰 대상을 비운다.</summary>
    private void StopObservingPlayerAP()
    {
        if (observedPlayerAP != null)
        {
            observedPlayerAP.APChanged -= UpdatePlayerApDisplay;
            observedPlayerAP = null;
        }
    }

    /// <summary>
    /// BattleUnitAP.APChanged가 전달한 현재·최대 AP를 이미지 비율과 선택적 수치 Text에 반영한다.
    /// 최대 AP가 0인 초기 상태에서는 0으로 나누지 않고 채움 비율을 0으로 표시한다.
    /// </summary>
    private void UpdatePlayerApDisplay(int currentAP, int maximumAP)
    {
        UpdateApCrystals(currentAP, maximumAP);
        if (apValueText != null)
        {
            apValueText.text = $"{currentAP}/{maximumAP}";
        }
    }

    /// <summary>
    /// 크리스탈 목록이 연결돼 있으면 인덱스가 최대 AP 미만인 크리스탈만 활성화하고,
    /// 현재 AP보다 작은 인덱스는 채워진 스프라이트, 그 이상은 빈 스프라이트로 표시한다.
    /// 최대 AP를 넘는 인덱스의 크리스탈은 비활성화해서 바 중 실제 보유한 칸만 보이게 한다.
    /// </summary>
    private void UpdateApCrystals(int currentAP, int maximumAP)
    {
        if (apCrystalImages == null || apCrystalImages.Length == 0)
        {
            return;
        }

        for (int i = 0; i < apCrystalImages.Length; i++)
        {
            Image crystal = apCrystalImages[i];
            if (crystal == null)
            {
                continue;
            }

            bool withinMax = i < maximumAP;
            crystal.gameObject.SetActive(withinMax);
            if (!withinMax)
            {
                continue;
            }

            bool isFilled = i < currentAP;
            Sprite targetSprite = isFilled ? apCrystalFilledSprite : apCrystalEmptySprite;
            if (targetSprite != null)
            {
                crystal.sprite = targetSprite;
            }
            // MP와 같은 규칙: 빈 이미지가 없으면 같은 크리스탈을 어둡게 표시한다.
            if (apCrystalEmptySprite == null)
            {
                crystal.color = isFilled ? Color.white : depletedCrystalColor;
            }
        }
    }
}
