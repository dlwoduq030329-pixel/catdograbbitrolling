using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public class OrbitSet
{
    public RectTransform centerPoint;     // 중심점 (회전 중심)
    public List<RectTransform> targetMarkers;  // 여러 개의 성공 타겟
    public GameObject setRoot;            // 이 세트를 묶은 GameObject (활성/비활성 전환용)
}


public class OrbitTimingGame : MonoBehaviour
{
    public List<OrbitSet> orbitSets = new List<OrbitSet>(); // 여러 쌍 등록
    public RectTransform movingDot;       // 하나의 공통 이동 점
    public float radius = 150f;
    public float rotateSpeed = 100f;
    public float successThreshold = 30f;
    public static bool IsRunning { get; private set; }
    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip suceessSound;
    public AudioClip failSound;
    public AudioClip allsuceessSound;

    private float currentAngle = 0f;
    private OrbitSet currentSet;
    private List<RectTransform> remainingTargets = new List<RectTransform>();

    [SerializeField]
    Treasure trea;

    void OnEnable()
    {
        StartGame();
        //ActivateRandomSet();
    }

    public void OnDisable()
    {
        IsRunning = false;
        StopAllCoroutines();
    }
    public void StartGame()
    {
        IsRunning = true;
        ActivateRandomSet();
    }
    void Update()
    {
        if (currentSet == null) return;

        // 회전 위치 업데이트
        currentAngle += rotateSpeed * Time.deltaTime;
        currentAngle %= 360f;

        float rad = currentAngle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        movingDot.anchoredPosition = currentSet.centerPoint.anchoredPosition + offset;

        if (Input.GetKeyDown(KeyCode.Space)&&trea.TryOpen)
        {
            Debug.Log("꾹꾹꾹꾹");
            TryCheckSuccess();
        }
    }

    void TryCheckSuccess()
    {
        if (remainingTargets.Count == 0) return;


        RectTransform closest = null;
        float closestDistance = float.MaxValue;

        foreach (var target in remainingTargets)
        {
            float dist = Vector2.Distance(movingDot.anchoredPosition, target.anchoredPosition);
            if (dist < closestDistance)
            {
                closest = target;
                closestDistance = dist;
            }
        }

        // 성공 체크 후 성공한 타겟 처리
        if (closestDistance <= successThreshold)
        {
            Debug.Log($"✅ 타겟 {closest.name} 성공!");
            if (audioSource != null && suceessSound != null)
                audioSource.PlayOneShot(suceessSound);
            remainingTargets.Remove(closest);
            SetAlpha(closest, 1f); // 불투명하게
            closest.GetComponent<Image>().raycastTarget = false;

            if (remainingTargets.Count == 0)
            {
                Debug.Log("🎉 모든 타겟 성공!");
                if (audioSource != null && allsuceessSound != null)
                    audioSource.PlayOneShot(allsuceessSound);
                //성공 처리
                trea.Success();
                if (movingDot != null)
                    movingDot.gameObject.SetActive(false);

                currentSet.setRoot.SetActive(false);
                currentSet = null;
                IsRunning = false;
                gameObject.SetActive(false);
                //StartCoroutine(RestartAfterDelay(1f));
            }
        }
        else
        {
            Debug.Log("❌ 실패! 다시 시작");
            if (audioSource != null && failSound != null)
                audioSource.PlayOneShot(failSound);
            trea.Fail();
            this.gameObject.SetActive(false);
           // StartCoroutine(RestartCurrentSet());
        }
    }

    void ActivateRandomSet()
    {
        // 전체 비활성화
        foreach (var set in orbitSets)
        {
            if (set.setRoot != null)
                set.setRoot.SetActive(false);
        }

        // 랜덤 세트 선택
        int rand = Random.Range(0, orbitSets.Count);
        currentSet = orbitSets[rand];
        currentSet.setRoot.SetActive(true);

        if (movingDot != null)
            movingDot.gameObject.SetActive(true);

        // 모든 타겟 복원 및 등록
        remainingTargets.Clear();
        foreach (var marker in currentSet.targetMarkers)
        {
            marker.gameObject.SetActive(true);
            remainingTargets.Add(marker);
            SetAlpha(marker, 0f); // 다시 반투명하게
        }

        currentAngle = 0f;
    }

    IEnumerator RestartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ActivateRandomSet();
    }

    IEnumerator RestartCurrentSet()
    {
        // 실패 시 리셋
        yield return null;

        /*      currentSet.setRoot.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        currentSet.setRoot.SetActive(true);

        // 타겟 복원
        remainingTargets.Clear();
        foreach (var marker in currentSet.targetMarkers)
        {
            marker.gameObject.SetActive(true);
            SetAlpha(marker, 0f); // 다시 반투명하게
            remainingTargets.Add(marker);
        }

        currentAngle = 0f;*/
    }

    void SetAlpha(RectTransform target, float alpha)
    {
        Image img = target.GetComponent<Image>();
        if (img != null)
        {
            Color color = img.color;
            color.a = alpha;
            img.color = color;
        }
    }
}
