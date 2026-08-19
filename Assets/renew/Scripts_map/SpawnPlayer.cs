using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnPlayer : MonoBehaviour
{
    [SerializeField]
    GameObject[] playerPrefab;
    [SerializeField]
    GameObject playerBody;
    [SerializeField]
    MapGenerator mapGenerator;

    [SerializeField]
    GameObject charactorStatus;
    [SerializeField]
    LoadingUI loading;
    [SerializeField]
    Canvas hudCanvas;

    private GameObject player;


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
        mapGenerator.StartGenerator();
        player = Instantiate(playerPrefab[charactorIndex], playerBody.transform);
        playerBody.GetComponent<CharactorStatus>().TribeSet(charactorIndex);
        PlayerDeck deck = playerBody.GetComponent<PlayerDeck>();
        deck.UICardInit();
        for (int i = 0; i < 5; i++)
        {
            deck.AddCardPool(i, 2);
        }
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

        while (!mapGenerator.IsGenerateEnd())
        {
            yield return null;
        }
        //���̵� �ƿ� �ڵ� �߰�
        //PlayerInfoInit();

        //ī�޶� �̵� �ڵ�
        Camera.main.GetComponent<CameraChase>().InitTarget(player);

        // 페이드 아웃은 PlayerPosInit 시작 시점에 이미 걸어뒀다(맵 생성 과정을 가리기 위해).
        // 여기서는 화면이 완전히 검게 덮인 뒤 HUD를 켜고 다시 밝게 되돌리기만 하면 된다.
        yield return new WaitForSeconds(1f);
        hudCanvas.gameObject.SetActive(true);

        loading.FadeIn();
    }
}