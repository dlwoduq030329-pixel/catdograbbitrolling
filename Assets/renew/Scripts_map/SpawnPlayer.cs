using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnPlayer : MonoBehaviour
{
    [Tooltip("캐릭터 선택이 완료된 후 캐릭터를 소환 담당")]
    [Header("코드 설명용 변수. 마우스를 올려 확인.")]
    [SerializeField]
    bool CODE_EXPLAIN;

    [SerializeField]
    GameObject[] playerPrefab;
    [SerializeField]
    GameObject playerBody;
    [SerializeField]
    MapGenerator mapGenerator;
    [SerializeField]
    NewMapGenerator newMapGenerator;

    [SerializeField]
    GameObject charactorStatus;
    [SerializeField]
    LoadingUI loading;
    [SerializeField]
    Canvas hudCanvas;
    [Header("전투 맵 위 Player 배치")]
    [Tooltip("생성된 캐릭터 Prefab의 원본 Scale에 곱할 값입니다. Yeop 타일과 캐릭터 크기를 Inspector에서 맞출 때 사용합니다.")]
    [SerializeField, Min(0.01f)]
    float spawnedPlayerScaleMultiplier = 1f;
    [Tooltip("Player Body 기준으로 생성된 캐릭터 루트를 위로 올릴 높이입니다. 타일 윗면과 발 위치를 맞춥니다.")]
    [SerializeField]
    float defaultSpawnHeight = 0.5f;

    [SerializeField]
    bool isNew;

    private GameObject player;

    /// <summary>
    /// PlayerPosInit이 실제로 생성한 전투 Player다.
    /// 다른 시스템이 Player Body의 자식 순서를 추측하지 않고 생성 결과를 직접 받을 때 사용한다.
    /// </summary>
    public GameObject SpawnedPlayer => player;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PlayerPosInit(int charactorIndex)
    {
        // 맵 생성이 끝난 뒤에야 화면을 가렸던 게 문제였다(LoadingCanvas의 CanvasGroup이
        // 기본적으로 alpha 0/자식 Panel 비활성 상태라 아무것도 가려지지 않은 채 맵이 만들어지는
        // 과정이 그대로 보였다). 캐릭터를 확정해 게임이 시작되는 이 시점에 곧바로 페이드 아웃을
        // 시작해서 맵 생성 과정 자체가 화면에 보이지 않도록 한다. 기존 1초 페이드는 그 1초
        // 동안에도 맵이 비쳐 보였기 때문에(느린 페이드=화면이 서서히 불투명해짐), 게임 시작
        // 페이드 아웃만 0.15초로 훨씬 빠르게 해서 거의 즉시 화면을 덮는다.
        loading.FadeOut(0.15f);
        if(isNew)
        {
            newMapGenerator.StartGenerator();
        }else
        {
            mapGenerator.StartGenerator();
        }
        player = Instantiate(playerPrefab[charactorIndex], playerBody.transform);
        // 캐릭터 Prefab마다 이미 설정된 원본 비율은 유지하고, 맵 규격에 필요한 공통 배율만 추가로 적용한다.
        // Prefab Asset 자체를 수정하지 않으므로 다른 Scene에서 사용하는 캐릭터 크기에는 영향을 주지 않는다.
        player.transform.localScale *= spawnedPlayerScaleMultiplier;
        Vector3 spawnLocalPosition = player.transform.localPosition;
        // 맵 타일 Mesh/Collider 높이가 바뀌어도 Scene별 Inspector 값으로 발 위치를 조정할 수 있게
        // Player Body의 고정 위치가 아니라 생성된 캐릭터 루트의 로컬 Y만 보정한다.
        spawnLocalPosition.y = defaultSpawnHeight;
        player.transform.localPosition = spawnLocalPosition;
        playerBody.GetComponent<CharactorStatus>().TribeSet(charactorIndex);
        PlayerDeck deck = playerBody.GetComponent<PlayerDeck>();
        // 신규 Player의 기본 카드 상태는 PlayerDeck 한 곳에서 구성한다.
        // SpawnPlayer는 카드 수량과 장착 슬롯 규칙을 알지 않고 초기화 시점만 결정한다.
        deck.InitializeDefaultCards(5, 2);
        StartCoroutine(waitUnitMApGen());
    }

    public void PlayerInfoInit(int[] playerstatus)
    {


        charactorStatus.GetComponent<CharactorStatus>().InitStatus(playerstatus[0],
                                                          playerstatus[1],
                                                          playerstatus[2],
                                                          playerstatus[3],
                                                          playerstatus[4],
                                                          playerstatus[5]
                                                          );
    }

    public IEnumerator waitUnitMApGen()
    {

        if(isNew)
        {
            while (!newMapGenerator.IsGenerateEnd())
            {
                yield return null;
            }

        }
        else
        {

            while (!mapGenerator.IsGenerateEnd())
            {
                yield return null;
            }
        }

        //���̵� �ƿ� �ڵ� �߰�
        //PlayerInfoInit();

        //ī�޶� �̵� �ڵ�
        Camera.main.GetComponent<CameraChase>().InitTarget(player);

        // 페이드 아웃은 PlayerPosInit 시작 시점에 이미 걸어뒀다(맵 생성 과정을 가리기 위해).
        // 여기서는 화면이 완전히 검게 덮인 뒤 HUD를 켜고 다시 밝게 되돌리기만 하면 된다.
        yield return new WaitForSeconds(1f);
        hudCanvas.gameObject.SetActive(true);
        
        yield return new WaitForSeconds(1f);
        FogOfWarManager.Instance.Reveal(
            player.transform.position
        );
        //RevealManager.Instance.StartReveal();
        //FogOfWarManager.Instance.SetPlayer(playerBody.transform);
        //FogOfWarManager.Instance.StartFog(); 
        loading.FadeIn();
    }
}
