# -*- coding: utf-8 -*-
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
        assert count == 1, (path, i, count, old[:120])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

p = "Assets/renew/Battle/Rewards/BattleChestRewardSystem.cs"
apply(p, [
    (
'''    private static Button CreateButton(RectTransform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = new GameObject(label + " Button", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(220f, 58f);
        rect.GetComponent<Image>().color = new Color(0.22f, 0.27f, 0.38f, 1f);
        Button button = rect.GetComponent<Button>();
        button.onClick.AddListener(action);
        TMP_Text text = CreateText(rect, "Label", Vector2.zero, rect.sizeDelta, 24f);
        text.text = label;
        return button;
    }

''',
''''''
    ),
])
