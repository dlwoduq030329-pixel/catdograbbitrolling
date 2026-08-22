using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 현재 Enemy 체력 바의 파란 실린더 이미지에 Enemy 행동력(MP) 비율을 표시한다.
/// BattleHealthBarFactory가 Enemy 체력 바를 런타임에 조립할 때 이 컴포넌트를 추가하고,
/// MP 채움 Image와 Enemy의 BattleUnitMP를 연결한다. Player MP UI에서는 사용하지 않는다.
/// BattleUnitMP.MPChanged 이벤트를 받으면 목표 비율을 바꾸고, 화면 비율은 지정 속도로 부드럽게 이동한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleManaBarView : MonoBehaviour
{
    [Header("Mp 데이터")]
    [InspectorName("표시할 Mp")]
    [FormerlySerializedAs("targetMP")]
    [SerializeField] private BattleUnitMP observedEnemyMana;

    [Header("Mp 바 UI")]
    [InspectorName("Mp 채움 이미지 (Filled)")]
    [FormerlySerializedAs("fillImage")]
    [SerializeField] private Image manaFillImage;

    [Header("표시 설정")]
    [InspectorName("Mp 바 변화 속도")]
    [FormerlySerializedAs("mpChangeSpeed")]
    [SerializeField, Min(0.01f)] private float manaRatioChangeSpeed = 4f;

    private float targetManaRatio = 1f;
    private float displayedManaRatio = 1f;
    private bool hasInitializedDisplayedRatio;

    /// <summary>
    /// View가 활성화될 때 Inspector 또는 Factory가 전달해 둔 Enemy MP를 다시 연결한다.
    /// Bind 내부에서 기존 구독을 먼저 해제하므로 재활성화되어도 이벤트가 중복 등록되지 않는다.
    /// </summary>
    private void OnEnable()
    {
        Bind(observedEnemyMana);
    }

    /// <summary>View가 비활성화된 동안 Enemy MP 변경 이벤트를 계속 받지 않도록 구독을 해제한다.</summary>
    private void OnDisable()
    {
        UnsubscribeFromEnemyMana();
    }

    /// <summary>
    /// 최초 비율이 준비된 뒤 화면 비율을 목표 MP 비율 쪽으로 매 프레임 이동시킨다.
    /// 게임 일시정지 중에도 UI 연출이 이어지도록 unscaledDeltaTime을 사용한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!hasInitializedDisplayedRatio)
        {
            return;
        }

        displayedManaRatio = Mathf.MoveTowards(
            displayedManaRatio,
            targetManaRatio,
            manaRatioChangeSpeed * Time.unscaledDeltaTime);
        ApplyManaRatioToFillImage(displayedManaRatio);
    }

    /// <summary>
    /// BattleHealthBarFactory가 Enemy 체력 바 아트 내부에서 찾은 `Mp` 채움 이미지를 연결한다.
    /// 현재는 Inspector 직접 참조가 아니라 Enemy 체력 바 런타임 조립 경로에서 호출된다.
    /// </summary>
    public void ConfigureManaFillImage(Image enemyManaFillImage)
    {
        manaFillImage = enemyManaFillImage;
    }

    /// <summary>
    /// 표시할 Enemy MP 컴포넌트를 교체한다. 이전 Enemy의 이벤트 구독을 해제한 뒤 새 MP 변경 이벤트를
    /// 구독하고, 현재 MP를 읽어 최초 또는 새로운 목표 비율을 즉시 준비한다.
    /// </summary>
    public void Bind(BattleUnitMP enemyMana)
    {
        UnsubscribeFromEnemyMana();
        observedEnemyMana = enemyMana;

        if (observedEnemyMana != null)
        {
            observedEnemyMana.MPChanged += OnEnemyManaChanged;
        }

        RefreshTargetManaRatio();
    }

    /// <summary>
    /// 연결된 Enemy의 현재 MP/최대 MP로 목표 비율을 다시 계산한다.
    /// 최초 연결에서는 화면 비율도 목표값으로 즉시 맞추고, 이후 변경에서는 목표값만 바꿔 LateUpdate가
    /// 기존 화면 비율에서 새 비율까지 부드럽게 이동하도록 한다.
    /// </summary>
    public void RefreshTargetManaRatio()
    {
        if (observedEnemyMana == null)
        {
            targetManaRatio = 0f;
            displayedManaRatio = 0f;
            hasInitializedDisplayedRatio = true;
            if (manaFillImage != null)
            {
                manaFillImage.fillAmount = 0f;
            }

            return;
        }

        float currentManaRatio = observedEnemyMana.MaxMP > 0
            ? (float)observedEnemyMana.CurrentMP / observedEnemyMana.MaxMP
            : 0f;

        targetManaRatio = Mathf.Clamp01(currentManaRatio);
        if (!hasInitializedDisplayedRatio)
        {
            displayedManaRatio = targetManaRatio;
            hasInitializedDisplayedRatio = true;
        }

        ApplyManaRatioToFillImage(displayedManaRatio);
    }

    /// <summary>0~1로 제한한 화면 MP 비율을 Unity Filled Image의 fillAmount에 적용한다.</summary>
    private void ApplyManaRatioToFillImage(float manaRatio)
    {
        if (manaFillImage == null)
        {
            return;
        }

        float clampedManaRatio = Mathf.Clamp01(manaRatio);
        manaFillImage.type = Image.Type.Filled;
        manaFillImage.fillAmount = clampedManaRatio;
    }

    /// <summary>
    /// Enemy의 이동·공격으로 BattleUnitMP가 변경됐을 때 호출된다.
    /// 현재 구현은 이벤트 매개변수를 직접 계산에 쓰지 않고 연결된 MP에서 최신 값을 다시 읽어 목표 비율을 갱신한다.
    /// </summary>
    private void OnEnemyManaChanged(int currentMana, int maximumMana)
    {
        RefreshTargetManaRatio();
    }

    /// <summary>현재 연결된 Enemy MP의 변경 이벤트에서 이 View를 제거한다.</summary>
    private void UnsubscribeFromEnemyMana()
    {
        if (observedEnemyMana == null)
        {
            return;
        }

        observedEnemyMana.MPChanged -= OnEnemyManaChanged;
    }
}
