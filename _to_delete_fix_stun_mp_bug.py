# -*- coding: utf-8 -*-
import os

def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

def apply(path, replacements):
    content = load(path)
    for i, (old, new) in enumerate(replacements, start=1):
        count = content.count(old)
        assert count == 1, (path, i, count, old[:80])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

p = "Assets/renew/Battle/Core/BattleGameManager.cs"
apply(p, [
    (
'''        // 기절한 Player는 MP 회복, 주사위 입력, 카드 드로우 없이 바로 Enemy 턴으로 넘긴다.
        if (playerTurnSkipped)
        {
            isPlayerTurnActive = false;
            hasRolledDiceThisTurn = false;
            currentDiceRoll = 0;
            currentTurnNumber++;
            // 버튼과 카드 사용 가능 상태를 먼저 잠근 뒤 Enemy 순차 행동을 시작한다.
            SyncTurnUI();
            StartCoroutine(RunEnemyTurnSequence());
            return;
        }''',
'''        // 기절한 Player는 MP 회복, 주사위 입력, 카드 드로우 없이 바로 Enemy 턴으로 넘긴다.
        if (playerTurnSkipped)
        {
            isPlayerTurnActive = false;
            hasRolledDiceThisTurn = false;
            currentDiceRoll = 0;
            currentTurnNumber++;
            // 실제 버그 수정(2026-08-22, 사용자 확인): 기절로 Player 턴을 건너뛰어도 바로 이어지는
            // Enemy 턴은 그대로 진행되므로, 여기서도 다음 Enemy 턴 MP를 새로 굴려둬야 한다.
            // 이 호출이 없으면 EnemyTurnActor.RollTurnMP()가 이번 라운드에 한 번도 호출되지 않아
            // Enemy가 그 이전 Player 턴에서 굴렸던 오래된 MP 값을 그대로 들고 행동하게 된다.
            PrepareEnemiesForNextTurn();
            // 버튼과 카드 사용 가능 상태를 먼저 잠근 뒤 Enemy 순차 행동을 시작한다.
            SyncTurnUI();
            StartCoroutine(RunEnemyTurnSequence());
            return;
        }'''
    ),
])
