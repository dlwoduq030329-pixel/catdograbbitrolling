# -*- coding: utf-8 -*-
import os

def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

# 1) BattlePlayerActionController.cs - remove event + RaiseActionConfirmed helper
p1 = "Assets/renew/Battle/Player/BattlePlayerActionController.cs"
c1 = load(p1)
old1 = '''    /// <summary>확정된 행동 결과를 구독자에게 전달하는 이벤트.</summary>
    public event System.Action<BattleActionResult> ActionConfirmed;

    /// <summary>하위 행동 플로우(BattleUnitAttackFlow 등)가 확정 결과를 대신 발생시킬 때 쓰는 내부 통로.</summary>
    internal void RaiseActionConfirmed(BattleActionResult result) => ActionConfirmed?.Invoke(result);

'''
assert c1.count(old1) == 1, c1.count(old1)
c1 = c1.replace(old1, "", 1)
save(p1, c1)

# 2) BattleUnitAttackFlow.cs - remove RaiseActionConfirmed call
p2 = "Assets/renew/Battle/Player/BattleUnitAttackFlow.cs"
c2 = load(p2)
old2 = '''        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);
        owner.RaiseActionConfirmed(result);

        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"기본 공격 확정:'''
new2 = '''        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);

        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"기본 공격 확정:'''
assert c2.count(old2) == 1, c2.count(old2)
c2 = c2.replace(old2, new2, 1)
save(p2, c2)

# 3) BattleUnitCardFlow.cs - remove RaiseActionConfirmed call
p3 = "Assets/renew/Battle/Player/BattleUnitCardFlow.cs"
c3 = load(p3)
old3 = '''        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);
        FindFirstObjectByType<BattleCardPanelToggle>()?.Hide();
        owner.RaiseActionConfirmed(result);

        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"카드 사용 확정:'''
new3 = '''        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);
        FindFirstObjectByType<BattleCardPanelToggle>()?.Hide();

        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"카드 사용 확정:'''
assert c3.count(old3) == 1, c3.count(old3)
c3 = c3.replace(old3, new3, 1)
save(p3, c3)

print("OK: ActionConfirmed removed from all 3 files")
