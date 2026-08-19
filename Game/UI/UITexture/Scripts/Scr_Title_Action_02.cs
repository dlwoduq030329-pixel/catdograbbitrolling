using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Scr_Title_Action_02 : MonoBehaviour
{
    [Header("Intro Animation (Action 01)")]
    public Animator introAnimator;   //Animator
    public string introStateName = "Title_Intro"; // 실제 상태 이름

    [Header("Top Title Letters (Drop + Bounce)")]
    public RectTransform[] topLetters;     // 위에서 떨어지는 글자들

    [Header("Bottom Title Letters (Rotate Up)")]
    public RectTransform[] bottomLetters;  // 아래에서 회전 등장 글자들

    [Header("Title Root")]
    public RectTransform titleRoot;         // 타이틀 전체 부모

    [Header("Start Button")]
    public RectTransform startButton;
    public CanvasGroup startButtonCG;

    // ===== 고정 기준값 =====
    private const float TOP_START_Y = 1300f;
    private const float TOP_TARGET_Y = 415f;
    private const float BOTTOM_OFFSET_Y = -85f;   // 상단보다 85 아래
    private const float TITLE_TARGET_Y = 200f;
    void Start()
    {
        StartCoroutine(WaitIntroAndStart());
    }

    IEnumerator WaitIntroAndStart()
    {
        // Animator 초기화 대기
        yield return null;

        // 해당 상태가 재생될 때까지 대기
        AnimatorStateInfo state;
        do
        {
            state = introAnimator.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }
        while (!state.IsName(introStateName));

        // 애니메이션 끝날 때까지 대기
        while (state.normalizedTime < 1f)
        {
            state = introAnimator.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }

        // 이제 Action 02 시작
        StartCoroutine(TitleSequence());
    }
    public void StartTitleAnimation()
    {
        StartCoroutine(TitleSequence());
    }

    IEnumerator TitleSequence()
    {
        //yield return new WaitForSeconds(12f);
        PrepareTitleState();

        // 1️ 위 타이틀
        for (int i = 0; i < topLetters.Length; i++)
        {
            CanvasGroup cg = topLetters[i].GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 1f;

            StartCoroutine(DropBounce(topLetters[i]));
            yield return new WaitForSeconds(0.08f);
        }

        yield return new WaitForSeconds(0.5f);

        // 2️ 아래 타이틀
        for (int i = 0; i < bottomLetters.Length; i++)
        {
            CanvasGroup cg = bottomLetters[i].GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 1f;

            StartCoroutine(RotateUpAppear(bottomLetters[i]));
            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(LiftTitleToFixedY());
        yield return StartCoroutine(ButtonAppear());
    }
    void PrepareTitleState()
    {
        // 위 타이틀
        foreach (var letter in topLetters)
        {
            CanvasGroup cg = letter.GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 0f;

            Vector2 pos = letter.anchoredPosition;
            pos.y = TOP_START_Y;
            letter.anchoredPosition = pos;
        }

        // 아래 타이틀
        foreach (var letter in bottomLetters)
        {
            CanvasGroup cg = letter.GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 0f;

            Vector2 pos = letter.anchoredPosition;
            pos.y = TOP_TARGET_Y + BOTTOM_OFFSET_Y - 150f;
            letter.anchoredPosition = pos;

            letter.localRotation = Quaternion.Euler(0, 0, -90f);
            letter.localScale = Vector3.one * 1.25f; // ⭐ 조금 크게
        }

        // 시작 버튼
        startButtonCG.alpha = 0f;
        startButton.localScale = Vector3.zero;
    }
    void SetLettersAlpha(RectTransform[] letters, float alpha)
    {
        foreach (var letter in letters)
        {
            CanvasGroup cg = letter.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = alpha;
            }
        }
    }

    // ===============================
    // 위 글자 : 1300 → 415
    // ===============================
    IEnumerator DropBounce(RectTransform letter)
    {
        Vector2 startPos = new Vector2(letter.anchoredPosition.x, TOP_START_Y);
        Vector2 endPos = new Vector2(letter.anchoredPosition.x, TOP_TARGET_Y);

        float time = 0f;
        float duration = 0.7f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // 중력 느낌 (후반 가속)
            float gravityEase = Mathf.Pow(t, 2.5f);
            letter.anchoredPosition = Vector2.Lerp(startPos, endPos, gravityEase);

            yield return null;
        }

        // 오버슈트 + 반동
        yield return Move(letter, endPos + Vector2.down * 25f, 0.08f);
        yield return Move(letter, endPos + Vector2.up * 12f, 0.1f);
        yield return Move(letter, endPos, 0.12f);
    }

    IEnumerator Bounce(RectTransform rt)
    {
        Vector2 origin = rt.anchoredPosition;
        yield return Move(rt, origin + Vector2.down * 15f, 0.1f);
        yield return Move(rt, origin, 0.15f);
    }

    // ===============================
    // 아래 글자 : 아래 → 위 회전 (최종 Y 고정)
    // ===============================
    IEnumerator RotateUpAppear(RectTransform rt)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = new Vector2(startPos.x, TOP_TARGET_Y + BOTTOM_OFFSET_Y);

        Quaternion startRot = Quaternion.Euler(0, 0, -120f);
        Quaternion endRot = Quaternion.identity;

        Vector3 startScale = Vector3.one * 1.35f;
        Vector3 midScale = Vector3.one * 0.92f;
        Vector3 endScale = Vector3.one;

        float time = 0f;
        float duration = 0.65f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // Back Ease 느낌
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            rt.localRotation = Quaternion.Lerp(startRot, endRot, ease);
            rt.localScale = Vector3.Lerp(startScale, endScale, ease);

            yield return null;
        }

        // 스케일 반동
        rt.localScale = midScale;
        yield return new WaitForSeconds(0.05f);
        rt.localScale = endScale;
    }

    // ===============================
    // 타이틀 전체 이동 (절대 Y)
    // ===============================
    IEnumerator LiftTitleToFixedY()
    {
        Vector2 start = titleRoot.anchoredPosition;
        Vector2 end = start;
        end.y = TITLE_TARGET_Y;

        yield return Move(titleRoot, end, 0.4f);
    }

    // ===============================
    // 시작 버튼
    // ===============================
    IEnumerator ButtonAppear()
    {
        startButtonCG.alpha = 0f;
        startButton.localScale = Vector3.zero;

        float time = 0f;
        float duration = 0.4f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            startButton.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            startButtonCG.alpha = t;
            yield return null;
        }

        yield return StartCoroutine(Shake(startButton));
    }

    IEnumerator Shake(RectTransform rt)
    {
        Vector2 originPos = rt.anchoredPosition;
        Quaternion originRot = rt.localRotation;

        float time = 0f;
        float duration = 0.8f;          // 전체 흔들림 시간
        float frequency = 30f;          // 흔들림 빈도 (클수록 빠름)
        float maxMove = 50f;            // 초기 이동량
        float maxRot = 6f;              // 초기 회전량

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            //감쇠 곡선 (초반 강하고, 후반 약함)
            float damping = 1f - Mathf.SmoothStep(0f, 1f, t);

            // 사인파로 좌우 왕복
            float sin = Mathf.Sin(time * frequency);

            float moveX = sin * maxMove * damping;
            float rotZ = sin * maxRot * damping;

            // 위치 보간
            Vector2 targetPos = originPos + Vector2.right * moveX;
            rt.anchoredPosition = Vector2.Lerp(
                rt.anchoredPosition,
                targetPos,
                Time.deltaTime * 20f
            );

            //slerp로 회전
            Quaternion targetRot = Quaternion.Euler(0, 0, rotZ);
            rt.localRotation = Quaternion.Slerp(
                rt.localRotation,
                targetRot,
                Time.deltaTime * 15f
            );

            yield return null;
        }

        //원래 상태로 돌아감
        float settleTime = 0f;
        while (settleTime < 0.15f)
        {
            settleTime += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, originPos, settleTime / 0.15f);
            rt.localRotation = Quaternion.Slerp(rt.localRotation, originRot, settleTime / 0.15f);
            yield return null;
        }

        rt.anchoredPosition = originPos;
        rt.localRotation = originRot;
    }

    // ===============================
    // 공용 이동
    // ===============================
    IEnumerator Move(RectTransform rt, Vector2 target, float duration)
    {
        Vector2 start = rt.anchoredPosition;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(start, target, time / duration);
            yield return null;
        }

        rt.anchoredPosition = target;
    }
}
