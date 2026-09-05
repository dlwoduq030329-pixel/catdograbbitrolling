using UnityEngine;
using TMPro;

/// <summary>
/// BattleDamageNumberPresenter가 생성한 데미지 숫자 오브젝트 하나에 붙어서, 그 오브젝트가 죽을 때까지
/// (Initialize에서 받은 지속시간 동안) 매 프레임 스스로 위로 떠오르고 서서히 투명해지는 연출을 담당한다.
/// Presenter는 오브젝트를 만들고 초기값만 넘길 뿐, 실제 애니메이션 진행은 이 컴포넌트가 자기 자신의
/// Update에서 전부 처리하고 끝나면 스스로 Destroy(gameObject)까지 호출한다.
/// </summary>
public sealed class BattleFloatingDamageNumber : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Camera billboardCamera;
    private Vector3 startPosition;
    private Color baseColor;
    private float riseDistance;
    private float lifetimeSeconds;
    private float holdBeforeFadeRatio;
    private float elapsedSeconds;
    private bool initialized;

    /// <summary>
    /// BattleDamageNumberPresenter가 GameObject 생성 직후 한 번만 호출해 이번 숫자의 모든 연출 값을 넘긴다.
    /// holdBeforeFadeRatio는 전체 지속시간 중 "완전히 선명하게 유지되는 비율"이다(예: 0.5면 앞 절반은
    /// 선명하게 떠오르고, 뒤 절반 동안만 서서히 투명해진다). billboardCamera가 null이면 회전 보정 없이
    /// 생성 당시 방향을 그대로 유지한다(Camera.main을 못 찾은 극단적인 경우의 안전장치).
    /// </summary>
    public void Initialize(
        TextMeshPro targetTextMesh,
        Camera targetBillboardCamera,
        float targetRiseDistance,
        float targetLifetimeSeconds,
        float targetHoldBeforeFadeRatio)
    {
        textMesh = targetTextMesh;
        billboardCamera = targetBillboardCamera;
        startPosition = transform.position;
        baseColor = textMesh != null ? textMesh.color : Color.white;
        riseDistance = targetRiseDistance;
        lifetimeSeconds = Mathf.Max(0.05f, targetLifetimeSeconds);
        holdBeforeFadeRatio = Mathf.Clamp01(targetHoldBeforeFadeRatio);
        elapsedSeconds = 0f;
        initialized = true;

        // 생성된 첫 프레임부터 이미 카메라를 바라보게 만들어 둔다. Update를 한 번도 못 돌고 파괴되는
        // 극단적으로 짧은 지속시간이더라도 최소 한 프레임은 카메라를 향한 상태로 보이게 하기 위함이다.
        ApplyBillboardRotation();
    }

    private void Update()
    {
        if (!initialized)
        {
            // Initialize 없이 실수로 붙기만 한 경우 아무 것도 하지 않고 대기한다(방어 코드).
            return;
        }

        elapsedSeconds += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedSeconds / lifetimeSeconds);

        // 위치: 처음엔 빠르게 솟구쳤다가 점점 느려지는 감속 곡선(1 - (1-t)^2)을 써서, 등속으로 밀려
        // 올라가는 것보다 "맞아서 튕겨 나온" 느낌에 더 가깝게 만든다. riseDistance는 전체 이동 거리다.
        float easedProgress = 1f - (1f - progress) * (1f - progress);
        transform.position = startPosition + Vector3.up * (riseDistance * easedProgress);

        // 투명도: holdBeforeFadeRatio 구간까지는 완전히 선명하게 유지하다가, 그 이후부터 lifetime 끝까지
        // 선형으로 0까지 페이드한다. 앞부분을 선명하게 유지해야 숫자를 읽을 시간이 확보된다.
        if (textMesh != null)
        {
            float fadeProgress = holdBeforeFadeRatio >= 1f
                ? 0f
                : Mathf.Clamp01((progress - holdBeforeFadeRatio) / (1f - holdBeforeFadeRatio));
            float alpha = Mathf.Lerp(1f, 0f, fadeProgress);
            textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        }

        ApplyBillboardRotation();

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 텍스트 평면이 항상 카메라와 정면으로 마주하도록 카메라와 같은 회전값을 그대로 따라간다.
    /// LookAt 방식 대신 카메라 회전을 그대로 복사하는 이유는, TextMeshPro 3D 텍스트는 앞뒤가 뒤집혀
    /// 보이는 문제가 LookAt 계산 방향에 따라 생길 수 있는데, 카메라 회전을 그대로 쓰면 항상 카메라
    /// 화면과 평행한 평면이 되어 뒤집힘 걱정 없이 안전하기 때문이다.
    /// </summary>
    private void ApplyBillboardRotation()
    {
        if (billboardCamera != null)
        {
            transform.rotation = billboardCamera.transform.rotation;
        }
    }
}
