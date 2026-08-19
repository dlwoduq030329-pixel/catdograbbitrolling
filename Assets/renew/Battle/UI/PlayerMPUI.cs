using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>CharacterMP 변경 이벤트를 Filled Image와 선택적 수치 Text에 표시한다.</summary>
public class PlayerMPUI : MonoBehaviour
{
    [Header("플레이어 행동력 화면 참조")]
    [InspectorName("마나 채움형 이미지")]
    [SerializeField] private Image manaFillImage;
    [InspectorName("행동력 수치 텍스트(선택 사항)")]
    [SerializeField] private TMP_Text manaText;

    private CharacterMP boundMP;

    /// <summary>화면 오브젝트 파괴 시 행동력 변경 이벤트 구독을 안전하게 해제한다.</summary>
    private void OnDestroy()
    {
        Unbind();
    }

    /// <summary>표시할 CharacterMP를 교체하고 이벤트 구독을 안전하게 갱신한다.</summary>
    public void Bind(CharacterMP characterMP)
    {
        if (boundMP == characterMP)
        {
            Refresh(boundMP != null ? boundMP.CurrentMP : 0, boundMP != null ? boundMP.MaxMP : 0);
            return;
        }

        Unbind();
        boundMP = characterMP;

        if (boundMP != null)
        {
            boundMP.MPChanged += Refresh;
            Refresh(boundMP.CurrentMP, boundMP.MaxMP);
        }
        else
        {
            Refresh(0, 0);
        }
    }

    /// <summary>이전에 표시하던 캐릭터의 행동력 이벤트 연결을 제거한다.</summary>
    private void Unbind()
    {
        if (boundMP != null)
        {
            boundMP.MPChanged -= Refresh;
            boundMP = null;
        }
    }

    /// <summary>현재·최대 행동력을 채움형 이미지 비율과 선택적 수치 텍스트에 반영한다.</summary>
    private void Refresh(int current, int maximum)
    {
        if (manaFillImage != null)
        {
            manaFillImage.fillAmount = maximum > 0 ? (float)current / maximum : 0f;
        }

        if (manaText != null)
        {
            manaText.text = $"{current} / {maximum}";
        }
    }
}
