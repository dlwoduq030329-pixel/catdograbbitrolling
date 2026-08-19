using UnityEngine;

/// <summary>
/// XZ 평면 거리를 기준으로 Player가 감지 범위 안에 있는지 지속적으로 확인한다.
/// 감지 결과만 제공하며 Target 기억과 경보 전파는 EnemyAwareness가 담당한다.
/// </summary>
public class EnemyDetector : MonoBehaviour
{
    [Header("플레이어 감지 설정")]
    [InspectorName("감지 거리")]
    [SerializeField] private float detectRange = 5f;
    [InspectorName("감지 기준점")]
    [SerializeField] private Transform eyePoint;

    [Header("대상 참조")]
    [InspectorName("플레이어 대상")]
    [SerializeField] private Transform playerTarget;

    public float DetectRange => detectRange;
    public bool IsDetectingPlayer { get; private set; }
    public Transform PlayerTarget => playerTarget;

    /// <summary>컴포넌트를 처음 추가할 때 감지 기준점을 자기 Transform으로 자동 지정한다.</summary>
    private void Reset()
    {
        eyePoint = transform;
    }

    /// <summary>감지 기준점 참조가 비어 있으면 적 오브젝트 위치를 대신 사용한다.</summary>
    private void Awake()
    {
        if (eyePoint == null)
        {
            eyePoint = transform;
        }
    }

    /// <summary>매 프레임 플레이어를 찾고 XZ 평면 거리를 계산해 현재 감지 여부를 갱신한다.</summary>
    private void Update()
    {
        if (playerTarget == null)
        {
            if (TryFindPlayer(out Transform foundPlayer))
            {
                playerTarget = foundPlayer;
            }
            else
            {
                IsDetectingPlayer = false;
                return;
            }
        }

        float distance = GetPlanarDistance(GetOriginPosition(), playerTarget.position);
        IsDetectingPlayer = distance <= detectRange;
    }

    /// <summary>BattleGameManager가 생성된 Player Transform을 전달한다.</summary>
    public void SetPlayerTarget(Transform target)
    {
        playerTarget = target;
    }

    /// <summary>DB에서 읽은 종류별 감지 거리를 적용한다.</summary>
    public void ConfigureDetectRange(float value)
    {
        detectRange = Mathf.Max(0f, value);
    }

    /// <summary>전달받은 대상이 현재 감지 범위 안인지 즉시 계산한다.</summary>
    public bool CanDetectPlayer(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return GetPlanarDistance(GetOriginPosition(), target.position) <= detectRange;
    }

    /// <summary>감지 거리 계산에 사용할 기준점의 월드 위치를 반환한다.</summary>
    private Vector3 GetOriginPosition()
    {
        return eyePoint != null ? eyePoint.position : transform.position;
    }

    /// <summary>높이 차이를 제외한 XZ 평면상의 두 위치 간 거리를 반환한다.</summary>
    private float GetPlanarDistance(Vector3 from, Vector3 to)
    {
        Vector2 difference = new Vector2(from.x - to.x, from.z - to.z);
        return difference.magnitude;
    }

    /// <summary>플레이어 태그를 사용해 아직 배포받지 못한 플레이어 참조를 보완한다.</summary>
    private bool TryFindPlayer(out Transform foundPlayer)
    {
        foundPlayer = null;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            return false;
        }

        foundPlayer = playerObject.transform;
        return true;
    }
}
