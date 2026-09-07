using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fog(안개) 시스템의 "탐험 여부"를 이용해 Enemy/상점/상자 같은 오브젝트의 렌더링을 켜고 끈다.
/// 판정 기준은 실시간 시야 반경이 아니라 FogOfWarManager.IsWorldPositionRevealed()이며,
/// 자신이 서 있는 위치가 지금까지 한 번이라도 밝혀졌으면 그 순간부터 보이도록 처리한다.
/// (Enemy처럼 이동하는 오브젝트는 매 프레임 재판정하므로, 탐험되지 않은 타일로 다시 들어가면
/// 다시 숨을 수 있다 - 위치 자체가 계속 바뀌는 오브젝트라 스냅샷이 아니라 실시간 재판정이며,
/// 이건 버그가 아니라 설계 의도다.)
/// 출구처럼 항상 보여야 하는 오브젝트에는 이 컴포넌트를 부착하지 않는다.
///
/// 개별 오브젝트가 각자 Update()로 매 프레임 갱신하는 대신, 활성화된 모든 인스턴스를
/// 정적 리스트(registered)에 모아두고 FogOfWarManager가 자기 Update() 안에서 이 리스트를
/// 한 번에 순회하며 RefreshAll()을 호출한다. Enemy/상점/상자 수가 아무리 늘어나도
/// Unity가 실제로 매 프레임 호출하는 Update()는 FogOfWarManager 하나뿐이라 더 가볍다.
/// </summary>
public class FogRevealVisibility : MonoBehaviour
{
    private static readonly List<FogRevealVisibility> registered = new List<FogRevealVisibility>();

    /// <summary>
    /// 디버그용 강제 전체 공개 스위치. FogOfWarManager가 F8 입력을 받으면 이 값을 토글한다.
    /// true인 동안은 실제 탐험 여부(fogPixels)와 무관하게 모든 FogRevealVisibility 오브젝트를
    /// 보이는 것으로 취급한다 - 지형 Fog 텍스처 자체는 전혀 건드리지 않고, 오브젝트 쪽 판정만
    /// 임시로 덮어쓰는 방식이라 실제 설계(탐험 기준)는 그대로 유지된다.
    /// </summary>
    public static bool DebugForceRevealAll;

    [Header("가시성 대상")]
    [Tooltip("비워두면 자기 자신과 자식의 Renderer를 자동으로 모두 찾는다.")]
    [SerializeField] private Renderer[] targetRenderers;

    [Tooltip("Renderer 외에 같이 켜고 꺼야 하는 오브젝트(예: HP바 UI 루트).")]
    [SerializeField] private GameObject[] extraVisualRoots;

    private bool isRevealedCache;
    private bool hasEvaluatedOnce;

    /// <summary>Renderer 목록을 비워둔 경우 자기 자신과 자식에서 자동으로 채운다.</summary>
    private void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    /// <summary>활성화되는 순간 공용 목록에 등록하고, 첫 프레임부터 상태가 맞도록 즉시 한 번 판정한다.</summary>
    private void OnEnable()
    {
        registered.Add(this);
        hasEvaluatedOnce = false;
        Refresh();
    }

    /// <summary>비활성화/파괴 시 공용 목록에서 제거해 죽은 참조가 남지 않게 한다.</summary>
    private void OnDisable()
    {
        registered.Remove(this);
    }

    /// <summary>이 오브젝트 하나의 가시성을 지금 시점 기준으로 다시 판정해서 반영한다.</summary>
    public void Refresh()
    {
        ApplyVisibility(EvaluateRevealed());
    }

    /// <summary>지금 화면에 보이는 상태인지(디버그 강제 공개 포함) 다른 시스템이 조회할 수 있게 한다.
    /// 예: Enemy 의도 미리보기 선(BattleThreatLineView)이 안 보이는 Enemy의 선까지 그리지 않도록.</summary>
    public bool IsRevealed => isRevealedCache;

    /// <summary>
    /// Renderer 외에 같이 켜고 꺼야 하는 오브젝트를 런타임에 등록한다(예: Enemy 스폰 직후에야
    /// 만들어지는 HP바처럼, Awake 시점에는 아직 존재하지 않아 자동 수집이 불가능한 오브젝트).
    /// 등록과 동시에 지금까지 판정된 상태를 새 오브젝트에도 바로 맞춘다.
    /// </summary>
    public void SetExtraVisualRoots(GameObject[] roots)
    {
        extraVisualRoots = roots;

        if (extraVisualRoots != null)
        {
            foreach (GameObject root in extraVisualRoots)
            {
                if (root != null)
                {
                    root.SetActive(isRevealedCache);
                }
            }
        }
    }

    /// <summary>FogOfWarManager가 아직 준비되지 않았으면 안전하게 "안 보임"으로 처리한다.</summary>
    private bool EvaluateRevealed()
    {
        if (DebugForceRevealAll)
        {
            return true;
        }

        FogOfWarManager manager = FogOfWarManager.Instance;
        if (manager == null || !FogOfWarManager.IsReady)
        {
            return false;
        }

        return manager.IsWorldPositionRevealed(transform.position);
    }

    /// <summary>상태가 실제로 바뀔 때만 Renderer/보조 오브젝트를 갱신한다(불필요한 반복 대입 방지).</summary>
    private void ApplyVisibility(bool revealed)
    {
        if (hasEvaluatedOnce && isRevealedCache == revealed)
        {
            return;
        }

        hasEvaluatedOnce = true;
        isRevealedCache = revealed;

        if (targetRenderers != null)
        {
            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = revealed;
                }
            }
        }

        if (extraVisualRoots != null)
        {
            foreach (GameObject root in extraVisualRoots)
            {
                if (root != null)
                {
                    root.SetActive(revealed);
                }
            }
        }
    }

    /// <summary>
    /// 현재 활성화되어 등록된 모든 FogRevealVisibility 인스턴스를 한 번에 갱신한다.
    /// FogOfWarManager가 매 프레임 자기 Update() 안에서 이 함수 하나만 호출하면 된다.
    /// </summary>
    public static void RefreshAll()
    {
        for (int i = 0; i < registered.Count; i++)
        {
            FogRevealVisibility instance = registered[i];
            if (instance != null)
            {
                instance.Refresh();
            }
        }
    }
}
