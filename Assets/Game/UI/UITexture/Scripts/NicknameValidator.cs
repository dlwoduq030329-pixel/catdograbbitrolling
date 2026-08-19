using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JetBrains.Annotations;
using UnityEngine.SceneManagement;


public class NicknameValidator : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_Text guideText;
    [SerializeField] private Button confirmButton;

    [Header("Colors")]
    [SerializeField] private Color validColor = HexToColor("68A061");
    [SerializeField] private Color invalidColor = HexToColor("D14C36");

    private readonly string[] bannedWords =
    {
        "욕설", "비속어", "시발", "ㅅㅂ", "병신", "ㅂㅅ", "바보"
    };

    private bool isValid;

    private const int MIN_LENGTH = 2;
    private const int MAX_LENGTH = 10;

    /* =========================
       Lifecycle
       ========================= */

    private void OnEnable()
    {
        confirmButton.interactable = false;

        nicknameInput.onValueChanged.AddListener(ClampLength);
        nicknameInput.onEndEdit.AddListener(ValidateNickname);
    }

    private void OnDisable()
    {
        nicknameInput.onValueChanged.RemoveListener(ClampLength);
        nicknameInput.onEndEdit.RemoveListener(ValidateNickname);
    }

    /* =========================
       Length Clamp (실시간)
       ========================= */

    private void ClampLength(string value)
    {
        if (value.Length <= MAX_LENGTH)
            return;

        nicknameInput.text = value.Substring(0, MAX_LENGTH);
    }

    /* =========================
       Validation (입력 완료 후)
       ========================= */

    private void ValidateNickname(string nickname)
    {
        isValid = false;

        if (nickname.Length < MIN_LENGTH || nickname.Length > MAX_LENGTH)
        {
            SetGuide("닉네임은 2~10글자만 가능합니다.");
            return;
        }

        if (ContainsSpecialChar(nickname))
        {
            SetGuide("특수문자는 사용 불가능합니다.");
            return;
        }

        if (ContainsBannedWord(nickname))
        {
            SetGuide("욕설 및 비속어는 사용 불가능합니다.");
            return;
        }

        // 통과
        isValid = true;
        guideText.text = "사용 가능한 닉네임입니다.";
        DataConfig.playerName = nickname;
        guideText.color = validColor;
        confirmButton.interactable = true;
        confirmButton.onClick.AddListener(() => GetComponent<AudioSource>().Play());
    }

    /* =========================
       Helpers
       ========================= */

    private void SetGuide(string message)
    {
        guideText.text = message;
        guideText.color = invalidColor;
        confirmButton.interactable = false;
    }

    private bool ContainsSpecialChar(string text)
    {
        return !Regex.IsMatch(text, @"^[a-zA-Z0-9가-힣]+$");
    }

    private bool ContainsBannedWord(string text)
    {
        string lower = text.ToLower();

        foreach (var word in bannedWords)
        {
            if (lower.Contains(word))
                return true;
        }

        return false;
    }

    public void OnConfirm()
    {
        if (!isValid)
            return;

        //UIConfigManager.Instance.SetNickname(nicknameInput.text);
        DataConfig.PlayerName = nicknameInput.text;
        SceneManager.LoadScene(1);
    }

    private static Color HexToColor(string hex)
    {
        ColorUtility.TryParseHtmlString($"#{hex}", out Color color);
        return color;
    }
}
