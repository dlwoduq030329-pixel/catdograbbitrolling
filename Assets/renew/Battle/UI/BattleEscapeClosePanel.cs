using UnityEngine;

/// <summary>
/// 활성화된 UI 패널을 ESC 입력으로 닫는다.
/// 패널의 데이터 갱신과 열기 방식은 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEscapeClosePanel : MonoBehaviour
{
    [InspectorName("캐릭터 선택 제어기")]
    [SerializeField] private PlayerChoose playerChoose;

    private int enabledFrame;

    private void Awake()
    {
        ResolvePlayerChoose();
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        ResolvePlayerChoose();
    }

    private void Update()
    {
        if (Time.frameCount > enabledFrame && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    /// <summary>ESC 또는 UI 닫기 요청으로 관리 중인 패널을 비활성화하고 선택 포커스를 정리한다.</summary>
    public void Close()
    {
        if (playerChoose != null)
        {
            playerChoose.BackChoose();
            return;
        }

        gameObject.SetActive(false);
    }

    /// <summary>기존 캐릭터 선택 흐름을 유지하기 위해 PlayerChoose 참조를 확보한다.</summary>
    private void ResolvePlayerChoose()
    {
        if (playerChoose == null)
        {
            playerChoose = FindFirstObjectByType<PlayerChoose>(FindObjectsInactive.Include);
        }
    }
}
