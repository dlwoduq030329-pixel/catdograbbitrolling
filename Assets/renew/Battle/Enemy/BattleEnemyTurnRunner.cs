using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 전투에 등록된 Enemy를 기존 등록 순서대로 실행한다.
/// 턴 상태 변경은 담당하지 않으며 각 Enemy 행동이 끝날 때까지 기다리는 역할만 가진다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyTurnRunner : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maximumSecondsPerEnemy = 10f;
    /// <summary>가장 최근 RunAll 실행에서 실제로 이동/공격한 Enemy가 하나라도 있었는지 여부.
    /// BattleGameManager가 다음 Player 턴의 페이드·배너 표시 여부를 결정할 때 사용한다.</summary>
    public bool AnyEnemyActedLastRun { get; private set; }

    /// <summary>Registry의 Enemy를 우선 실행하고 이전 Scene에서는 전체 검색 결과를 사용한다.
    /// 카메라 포커스는 각 Enemy의 TakeTurn이 실제로 행동을 시작할 때만 스스로 옮긴다(대기만
    /// 하는 Enemy에는 카메라가 따라가지 않는다).</summary>
    public IEnumerator RunAll(BattleDataPool battleDataPool)
    {
        AnyEnemyActedLastRun = false;
        BattleCameraRig cameraRig = Camera.main != null ? Camera.main.GetComponent<BattleCameraRig>() : null;
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
                    if (enemy.ActedThisTurn) AnyEnemyActedLastRun = true;
                }
            }

            cameraRig?.ClearTemporaryFocus();
            yield break;
        }

        EnemyTurnActor[] fallbackEnemies = FindObjectsByType<EnemyTurnActor>(
            FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in fallbackEnemies)
        {
            if (enemy != null)
            {
                yield return RunEnemyWithTimeout(enemy, cameraRig);
                if (enemy.ActedThisTurn) AnyEnemyActedLastRun = true;
            }
        }

        cameraRig?.ClearTemporaryFocus();
    }

    /// <summary>한 적의 행동이 멈춰도 적 턴 전체가 영구 정지하지 않도록 실시간 제한을 둔다.</summary>
    private IEnumerator RunEnemyWithTimeout(EnemyTurnActor enemy, BattleCameraRig cameraRig)
    {
        bool completed = false;
        IEnumerator RunAndMarkComplete()
        {
            yield return enemy.TakeTurn(cameraRig);
            completed = true;
        }

        Coroutine actionRoutine = StartCoroutine(RunAndMarkComplete());
        float elapsed = 0f;
        while (!completed && elapsed < maximumSecondsPerEnemy)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (completed) yield break;

        StopCoroutine(actionRoutine);
        BattleCharacterAnimationBridge.PlayIdle(enemy.gameObject);
        Debug.LogError($"{enemy.name}: 적 행동 제한 시간을 초과하여 이번 행동을 강제 종료했습니다.", enemy);
    }
}
