using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enemy·Player HP 바와 같은 프리팹 인스턴스 안에서 함께 쓰이는 행동력(MP) 게이지 표시 컴포넌트다.
/// BattleUnitMP의 MPChanged 이벤트를 받아 Filled Image만 갱신한다.
/// BattleHealthBarView와 별개로 동작하므로 HP 바가 없는 오브젝트에도 단독으로 붙일 수 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleManaBarView : MonoBehaviour
{
    [Header("행동력 데이터")]
    [InspectorName("표시할 행동력")]
    [SerializeField] private BattleUnitMP targetMP;

    [Header("행동력 바 UI")]
    [InspectorName("행동력 채움 이미지 (Filled)")]
    [SerializeField] private Image fillImage;

    [Header("표시 설정")]
    [InspectorName("행동력 바 변화 속도")]
    [SerializeField, Min(0.01f)] private float mpChangeSpeed = 4f;

    private float targetRatio = 1f;
    private float displayedRatio = 1f;
    private bool hasInitializedRatio;

    private void OnEnable()
    {
        Bind(targetMP);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (!hasInitializedRatio)
        {
            return;
        }

        displayedRatio = Mathf.MoveTowards(
            displayedRatio,
            targetRatio,
            mpChangeSpeed * Time.unscaledDeltaTime);
        ApplyVisualRatio(displayedRatio);
    }

    /// <summary>코드에서 동적으로 조립한 바에 채움 이미지를 연결한다(Inspector 배선 없이 런타임 조립할 때 사용).</summary>
    public void ConfigureFillImage(Image image)
    {
        fillImage = image;
    }

    /// <summary>표시할 행동력 컴포넌트를 연결한다.</summary>
    public void Bind(BattleUnitMP mp)
    {
        Unsubscribe();
        targetMP = mp;

        if (targetMP != null)
        {
            targetMP.MPChanged += HandleMPChanged;
        }

        Refresh();
    }

    /// <summary>현재 행동력 비율을 즉시 다시 표시한다.</summary>
    public void Refresh()
    {
        if (targetMP == null)
        {
            targetRatio = 0f;
            displayedRatio = 0f;
            hasInitializedRatio = true;
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
            }

            return;
        }

        float ratio = targetMP.MaxMP > 0
            ? (float)targetMP.CurrentMP / targetMP.MaxMP
            : 0f;

        targetRatio = Mathf.Clamp01(ratio);
        if (!hasInitializedRatio)
        {
            displayedRatio = targetRatio;
            hasInitializedRatio = true;
        }

        ApplyVisualRatio(displayedRatio);
    }

    private void ApplyVisualRatio(float ratio)
    {
        if (fillImage == null)
        {
            return;
        }

        ratio = Mathf.Clamp01(ratio);
        fillImage.type = Image.Type.Filled;
        fillImage.fillAmount = ratio;
    }

    private void HandleMPChanged(int current, int max)
    {
        Refresh();
    }

    private void Unsubscribe()
    {
        if (targetMP == null)
        {
            return;
        }

        targetMP.MPChanged -= HandleMPChanged;
    }
}
