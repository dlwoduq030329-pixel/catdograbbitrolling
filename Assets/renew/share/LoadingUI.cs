using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingUI : MonoBehaviour
{
    [SerializeField]
    CanvasGroup loadingUI;

    private Coroutine fadeRoutine;
    public bool IsFading => fadeRoutine != null;


    // 기존에는 페이드 소요 시간이 1초로 고정돼 있어서, 게임 시작 시 화면을 가리는 데만
    // 1초가 걸려 그동안 맵이 만들어지는 모습이 그대로 보였다. 호출부마다 원하는 속도를
    // 지정할 수 있도록 duration 매개변수를 추가했다(기본값은 기존과 동일한 1초).
    public void FadeOut(float duration = 1f)
    {
        StartFade(1f, duration);
    }

    public void FadeIn(float duration = 1f)
    {
        StartFade(0f, duration);
    }

    /// <summary>화면이 완전히 가려질 때까지 기다릴 수 있는 전투 턴 전환용 페이드이다.</summary>
    public IEnumerator FadeToBlackRoutine(float duration = 1f)
    {
        StopActiveFade();
        yield return FadeCanvas(1f, duration);
    }

    public IEnumerator fadeImg(float targetalpha, float duration = 1f)
    {
        yield return FadeCanvas(targetalpha, duration);
    }

    private void StartFade(float targetAlpha, float duration)
    {
        StopActiveFade();
        fadeRoutine = StartCoroutine(FadeCanvas(targetAlpha, duration));
    }

    private void StopActiveFade()
    {
        if (fadeRoutine == null) return;
        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private IEnumerator FadeCanvas(float targetalpha, float duration = 1f)
    {
        if (loadingUI == null)
        {
            fadeRoutine = null;
            yield break;
        }

        loadingUI.gameObject.SetActive(true);
        float nowalpha = loadingUI.alpha;
        float nowTime = 0;
        float targetTime = Mathf.Max(0.0001f, duration);


        while (nowTime <= targetTime)
        {
            nowTime += Time.unscaledDeltaTime;
            loadingUI.alpha = Mathf.Lerp(nowalpha, targetalpha, nowTime / targetTime);
            yield return null;
        }

        loadingUI.alpha = targetalpha;

        if (loadingUI.alpha <= 0f)
        {
            loadingUI.gameObject.SetActive(false);
        }
        fadeRoutine = null;
    }
}
