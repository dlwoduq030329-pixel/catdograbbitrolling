using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 전투에 등록된 Enemy를 기존 등록 순서대로 실행한다.
/// 턴 상태 변경은 담당하지 않으며 각 Enemy 행동이 끝날 때까지 기다리는 역할만 가진다.
/// BattleGameManager.RunEnemyTurnSequence가 이 컴포넌트의 RunAll을 yield return하여 호출한다.
/// 따라서 이 클래스는 "누구 차례인지"를 결정하지 않고, 전달받은 Enemy 차례를 순차 실행하는 실행기다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyTurnRunner : MonoBehaviour
{
    [Header("안전 제한")]
    [InspectorName("적 1명당 최대 실행 시간 (초)")]
    [Tooltip("버그로 적 행동이 끝나지 않을 때 다음 턴으로 넘어가기 위한 안전 제한입니다.")]
    [SerializeField, Min(1f)] private float maximumSecondsPerEnemy = 10f;

    [Header("Enemy 턴 페이싱")]
    [InspectorName("한 칸 이동 시간 (초)")]
    [Tooltip("값이 클수록 적이 타일 사이를 천천히 이동합니다.")]
    [SerializeField, Min(0.01f)] private float movementSecondsPerTile = 0.35f;
    [InspectorName("행동 전 카메라 집중 시간 (초)")]
    [Tooltip("적이 실제 이동하거나 공격하기 전에 카메라가 적을 보여주는 시간입니다.")]
    [SerializeField, Min(0f)] private float focusLeadInSeconds = 0.5f;
    [InspectorName("공격 타격 시점 지연 (초)")]
    [Tooltip("공격 애니메이션을 시작한 뒤 실제 피해가 적용될 때까지의 시간입니다.")]
    [SerializeField, Min(0f)] private float attackImpactDelaySeconds = 0.3f;
    [InspectorName("행동 후 확인 시간 (초)")]
    [Tooltip("이동 또는 공격 결과를 플레이어가 확인할 수 있도록 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float afterActionSeconds = 0.4f;
    [InspectorName("적 사이 대기 시간 (초)")]
    [Tooltip("한 적의 행동이 끝난 뒤 다음 적이 행동하기 전까지의 간격입니다.")]
    [SerializeField, Min(0f)] private float betweenEnemiesSeconds = 0.25f;
    [InspectorName("적 턴 종료 후 대기 시간 (초)")]
    [Tooltip("마지막 적 행동이 끝난 뒤 플레이어 턴 연출로 넘어가기 전까지의 시간입니다.")]
    [SerializeField, Min(0f)] private float afterEnemyTurnSeconds = 0.7f;
    /// <summary>가장 최근 RunAll 실행에서 실제로 이동/공격한 Enemy가 하나라도 있었는지 여부.
    /// BattleGameManager가 다음 Player 턴의 페이드·배너 표시 여부를 결정할 때 사용한다.</summary>
    public bool AnyEnemyActedLastRun { get; private set; }

    /// <summary>
    /// BattleGameManager.RunEnemyTurnSequence가 매 적 턴마다 이 코루틴을 yield return으로 호출한다.
    /// Registry에 등록된 Enemy를 등록 순서대로 하나씩 실행하고, 각 Enemy 행동 코루틴이 끝날 때까지 기다린다.
    /// Registry가 비어 있는 이전 Scene(BattleSceneInstaller 미배치)에서는 호환을 위해 Scene 전체 검색
    /// 결과를 대신 사용한다. 현재 존재하는 4개 Battle Scene은 모두 Installer가 있어 이 fallback을 타지 않는다.
    /// 카메라 포커스는 각 Enemy의 TakeTurn이 실제로 행동을 시작할 때만 스스로 옮긴다(대기만
    /// 하는 Enemy에는 카메라가 따라가지 않는다).
    /// 주의: 여기서 받은 battleDataPool은 이 메서드 안(Enemy 목록 조회)에서만 쓰이고 RunEnemyWithTimeout이나
    /// EnemyTurnActor.TakeTurn에는 전달되지 않는다. 각 EnemyTurnActor는 TakeTurn 안에서 자기 자신의
    /// ResolveBattleDataPool()로 FindFirstObjectByType<BattleDataPool>을 따로 호출해 독립적으로 값을 구한다
    /// (2026-08-21 확인: 지금은 Scene에 BattleDataPool이 하나뿐이라 우연히 같은 인스턴스로 귀결되지만,
    /// 이 메서드 시그니처만 봐서는 그 사실을 추적할 수 없다. Enemy 폴더 통합 정리 때 이 dataPool을
    /// RunEnemyWithTimeout/TakeTurn까지 명시적으로 전달하도록 바꾸는 것을 검토한다).
    /// </summary>
    public IEnumerator RunAll(BattleDataPool battleDataPool)
    {
        AnyEnemyActedLastRun = false;

        // 지난 적 턴 실행 도중 죽은 Enemy는 즉시 목록에서 빠지지 않고 대기열에 쌓여 있을 수 있다.
        // 아무도 Enemies를 순회하지 않는 지금 이 시점에 실제로 제거해 참가 목록을 최신 상태로 만든다.
        battleDataPool?.Units?.DrainPendingUnregisters();

        BattleCameraRig cameraRig = Camera.main != null ? Camera.main.GetComponent<BattleCameraRig>() : null; // BattleCameraRig이 없으면 null을 전달하여 Enemy가 카메라를 옮기지 못하게 한다.
        if (battleDataPool != null &&
            battleDataPool.Units != null &&
            battleDataPool.Units.Enemies.Count > 0)
        {
            // Registry.Enemies는 내부 List를 그대로 노출하므로, 순회 도중 Enemy가 죽어
            // BattleUnitRegistry.UnregisterEnemy가 같은 리스트를 변경하면 컬렉션 변경 예외가 발생한다.
            // (현재는 Enemy가 자기 턴 중 죽는 경로가 없어 실제로 발생하지 않지만, 반격·자해 피해 등이
            // 추가되면 즉시 깨지는 구조라 순회 전에 스냅샷을 떠서 방어한다.)
            List<GameObject> enemySnapshot = new List<GameObject>(battleDataPool.Units.Enemies); 
            foreach (GameObject enemyObject in enemySnapshot)
            {
                if (enemyObject == null || !enemyObject.activeInHierarchy)
                {
                    continue;
                }

                EnemyTurnActor enemy = enemyObject.GetComponent<EnemyTurnActor>();
                if (enemy == null)
                {
                    enemy = enemyObject.GetComponentInChildren<EnemyTurnActor>();
                }

                if (enemy != null)
                {
                    yield return RunEnemyWithTimeout(enemy, cameraRig);
                    if (enemy.ActedThisTurn)
                    {
                        AnyEnemyActedLastRun = true;
                        yield return WaitRealtime(betweenEnemiesSeconds);
                    }
                }
            }

            cameraRig?.ClearTemporaryFocus(); // Enemy 턴 종료 후 카메라를 원래 위치로 되돌린다.
            if (AnyEnemyActedLastRun) yield return WaitRealtime(afterEnemyTurnSeconds); // 마지막 Enemy 행동 후 플레이어 턴 연출로 넘어가기 전까지 기다린다.
            yield break;
        }

        // battleDataPool.Units.Enemies가 비어 있는 구형 Scene에서만 여기로 내려온다.
        // Registry가 없으므로 등록 순서를 보장할 수 없고, Scene에 있는 모든 EnemyTurnActor를
        // FindObjectsByType으로 찾아 그 결과 순서 그대로(순서 보장 없음) 위와 동일하게 실행한다.
        EnemyTurnActor[] fallbackEnemies = FindObjectsByType<EnemyTurnActor>(
            FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in fallbackEnemies)
        {
            if (enemy != null)
            {
                yield return RunEnemyWithTimeout(enemy, cameraRig);
                if (enemy.ActedThisTurn)
                {
                    AnyEnemyActedLastRun = true;
                    yield return WaitRealtime(betweenEnemiesSeconds);
                }
            }
        }

        cameraRig?.ClearTemporaryFocus();
        if (AnyEnemyActedLastRun) yield return WaitRealtime(afterEnemyTurnSeconds);
    }

    /// <summary>
    /// 버그로 TakeTurn이 끝나지 않는 경우를 대비한 보험 코드다. 정상 상황에서는 이 제한에 걸리지 않는다.
    /// enemy.TakeTurn을 직접 yield하지 않고 별도 코루틴으로 감싸 실행한 뒤, maximumSecondsPerEnemy 동안
    /// 완료 여부를 매 프레임 확인한다. 시간 안에 못 끝나면 그 코루틴만 강제 종료하고 Idle로 되돌려
    /// 적 턴 전체가 영구 정지하지 않게 한다. 단, 이동 도중 강제 종료되면 Transform이 타일 사이 어중간한
    /// 위치에 멈출 수 있다 — 실제로 이 경로를 타면 위치·점유 타일 상태를 QA로 확인해야 한다.
    /// </summary>
    private IEnumerator RunEnemyWithTimeout(EnemyTurnActor enemy, BattleCameraRig cameraRig)
    {
        // EnemyTurnRunner가 관리하는 공용 페이싱 값(이동 속도, 카메라 집중·타격·행동 후 대기 시간)을
        // 이 Enemy에 적용한다. 사용자가 요청해 추가된 "이동 시간" 설정도 이 경로로 전달된다.
        enemy.ConfigurePacing(
            movementSecondsPerTile,
            focusLeadInSeconds,
            attackImpactDelaySeconds,
            afterActionSeconds);
        bool completed = false;

        // enemy.TakeTurn을 바로 yield하면 이 메서드 자체가 TakeTurn과 함께 멈춰버려 아래 시간 제한
        // 루프를 돌릴 수 없다. 그래서 TakeTurn을 별도 코루틴으로 띄워두고, 이 메서드는 별도로
        // "완료했는지"만 감시한다.
        IEnumerator RunAndMarkComplete()
        {
            yield return enemy.TakeTurn(cameraRig);
            completed = true;
        }

        Coroutine actionRoutine = StartCoroutine(RunAndMarkComplete());

        // Time.unscaledDeltaTime 기준으로 "TakeTurn 완료를 기다리며 흘려보낸 실제 시간"을 잰다.
        // 연출 진행률을 나타내는 다른 elapsed(예: DisappearRoutine의 침몰 애니메이션 경과 시간)와는
        // 다른 값이라 헷갈리지 않도록 이 함수 안에서는 secondsWaitedForCompletion으로 구분해 부른다.
        float secondsWaitedForCompletion = 0f;
        while (!completed && secondsWaitedForCompletion < maximumSecondsPerEnemy)
        {
            secondsWaitedForCompletion += Time.unscaledDeltaTime;
            yield return null;
        }

        if (completed) yield break;

        StopCoroutine(actionRoutine);
        BattleCharacterAnimationBridge.PlayIdle(enemy.gameObject);
        Debug.LogError($"{enemy.name}: 적 행동 제한 시간을 초과하여 이번 행동을 강제 종료했습니다.", enemy);
    }

    /// <summary>Time.timeScale과 무관하게 설정된 Enemy 턴 연출 간격을 기다린다.</summary>
    private static IEnumerator WaitRealtime(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
    }
}
