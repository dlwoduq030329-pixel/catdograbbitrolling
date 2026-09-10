using UnityEngine;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    // ============================================================
    // MAP
    // ============================================================

    [Header("Map")]
    [Tooltip("씬에 존재하는 NewMapGenerator를 드래그합니다.")]
    [SerializeField]
    private NewMapGenerator mapGenerator;


    // ============================================================
    // PLAYER
    // ============================================================

    [Header("Player")]
    [Tooltip("씬에 항상 존재하는 PlayerBody를 드래그합니다.")]
    [SerializeField]
    private Transform playerBody;

    [Tooltip("PlayerBody의 자식으로 런타임 생성되는 실제 캐릭터 이름")]
    [SerializeField]
    private string playerChildName = "Player";


    // ============================================================
    // FOG
    // ============================================================

    [Header("Fog")]
    [Range(0f, 1f)]
    [SerializeField]
    private float fogOpacity = 0.9f;

    [SerializeField]
    private int textureResolution = 1024;

    [SerializeField]
    private float defaultRevealRadius = 3f;

    [SerializeField]
    private float edgeSoftness = 0.35f;


    // ============================================================
    // REVEAL
    // ============================================================

    [Header("Reveal")]
    [Tooltip("첫 Reveal 이후 Player 이동에 따라 자동으로 새로운 영역을 밝힙니다.")]
    [SerializeField]
    private bool revealAutomatically = true;

    [Tooltip("이 거리 이상 이동하면 새로운 Reveal을 굽습니다.")]
    [SerializeField]
    private float revealUpdateDistance = 0.25f;


    // ============================================================
    // RUNTIME
    // ============================================================

    private Texture2D fogTexture;
    private Color32[] fogPixels;

    private int mapSizeX;
    private int mapSizeZ;

    private float blockDistance;

    // PlayerMap 실제 외곽
    private Vector2 mapWorldMin;
    private Vector2 mapWorldSize;

    // 실제 캐릭터
    private Transform playerTransform;

    // Reveal 정보
    private Vector3 currentRevealPosition;
    private Vector3 lastBakedRevealPosition;

    private float currentRevealRadius;

    private bool hasRevealPosition;
    private bool isReady;

    // ★ 핵심
    // false = Fog 시스템 자체가 완전히 비활성
    private bool fogActive;


    // ============================================================
    // SHADER PROPERTY IDs
    // ============================================================

    private static readonly int FogTextureID =
        Shader.PropertyToID("_FogTexture");

    private static readonly int MapWorldMinAndSizeID =
        Shader.PropertyToID("_MapWorldMinAndSize");

    private static readonly int FogOpacityID =
        Shader.PropertyToID("_FogOpacity");

    private static readonly int RevealWorldPositionAndRadiusID =
        Shader.PropertyToID("_RevealWorldPositionAndRadius");

    private static readonly int FogActiveID =
        Shader.PropertyToID("_FogActive");


    // ============================================================
    // PUBLIC
    // ============================================================

    public static bool IsReady
    {
        get
        {
            return Instance != null &&
                   Instance.isReady;
        }
    }

    public static bool IsFogActive
    {
        get
        {
            return Instance != null &&
                   Instance.fogActive;
        }
    }

    public float RevealRadius
    {
        get
        {
            return defaultRevealRadius;
        }
    }

    public Transform PlayerTransform
    {
        get
        {
            return playerTransform;
        }
    }


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        // MapGenerator와 PlayerBody는
        // 씬에 항상 존재하므로 Inspector에서 직접 지정한다.

        InitializeWhenMapReady();
    }


    // ============================================================
    // INITIALIZE
    // ============================================================

    private void InitializeWhenMapReady()
    {
        if (mapGenerator == null)
        {
            Debug.LogError(
                "[FogOfWar] NewMapGenerator가 지정되지 않았습니다."
            );

            return;
        }


        // --------------------------------------------------------
        // 맵 생성 완료 대기
        // --------------------------------------------------------

        if (!mapGenerator.IsGenerateEnd())
        {
            Invoke(
                nameof(InitializeWhenMapReady),
                0.1f
            );

            return;
        }


        // --------------------------------------------------------
        // Map 정보
        // --------------------------------------------------------

        mapSizeX =
            mapGenerator.GetMapSizeX();

        mapSizeZ =
            mapGenerator.GetMapSizeZ();

        blockDistance =
            mapGenerator.GetBlockDistance();


        if (mapSizeX <= 0 ||
            mapSizeZ <= 0 ||
            blockDistance <= 0f)
        {
            Debug.LogError(
                "[FogOfWar] Map 크기 또는 Block Distance가 올바르지 않습니다."
            );

            return;
        }


        // --------------------------------------------------------
        // Map World Bounds
        // --------------------------------------------------------

        float halfX =
            (mapSizeX - 1) *
            blockDistance *
            0.5f;

        float halfZ =
            (mapSizeZ - 1) *
            blockDistance *
            0.5f;


        // 실제 PlayerMap 외곽
        mapWorldMin =
            new Vector2(
                -halfX -
                blockDistance * 0.5f,

                -halfZ -
                blockDistance * 0.5f
            );


        mapWorldSize =
            new Vector2(
                mapSizeX *
                blockDistance,

                mapSizeZ *
                blockDistance
            );


        // --------------------------------------------------------
        // Texture
        // --------------------------------------------------------

        textureResolution =
            Mathf.Max(
                32,
                textureResolution
            );


        fogTexture =
            new Texture2D(
                textureResolution,
                textureResolution,
                TextureFormat.RGBA32,
                false,
                false
            );


        fogTexture.name =
            "Runtime Fog Of War";

        fogTexture.wrapMode =
            TextureWrapMode.Clamp;

        fogTexture.filterMode =
            FilterMode.Bilinear;

        fogTexture.anisoLevel = 0;


        fogPixels =
            new Color32[
                textureResolution *
                textureResolution
            ];


        // --------------------------------------------------------
        // ★ 처음에는 완전히 미공개
        // --------------------------------------------------------

        ClearFogPixels();

        UploadFogTexture();


        // --------------------------------------------------------
        // Ready
        // --------------------------------------------------------

        isReady = true;

        // ★ 절대 자동으로 Fog를 켜지 않는다.
        fogActive = false;

        hasRevealPosition = false;

        currentRevealPosition =
            Vector3.zero;

        lastBakedRevealPosition =
            Vector3.zero;

        currentRevealRadius =
            defaultRevealRadius;


        // --------------------------------------------------------
        // Player 연결
        // --------------------------------------------------------

        ResolvePlayerChild();


        ApplyShaderGlobals();


        Debug.Log(
            "[FogOfWar] Initialized\n" +
            $"Map = {mapSizeX} x {mapSizeZ}\n" +
            $"BlockDistance = {blockDistance}\n" +
            $"WorldMin = {mapWorldMin}\n" +
            $"WorldSize = {mapWorldSize}\n" +
            "Fog Active = false"
        );
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // --------------------------------------------------------
        // 아직 초기화되지 않음
        // --------------------------------------------------------

        if (!isReady)
            return;


        // --------------------------------------------------------
        // Player가 아직 생성되지 않았다면 연결 시도
        // --------------------------------------------------------

        if (playerTransform == null)
        {
            ResolvePlayerChild();
        }


        // --------------------------------------------------------
        // ★ 첫 Reveal 전에는 여기서 끝
        //
        // Player가 있어도
        // Fog를 켜지 않는다.
        // --------------------------------------------------------

        if (!fogActive)
            return;


        if (playerTransform == null)
            return;


        // --------------------------------------------------------
        // 현재 Player 위치
        // --------------------------------------------------------

        currentRevealPosition =
            playerTransform.position;


        // Shader에 현재 위치 전달
        ApplyShaderGlobals();


        // --------------------------------------------------------
        // 자동 Reveal
        // --------------------------------------------------------

        if (!revealAutomatically)
            return;


        Vector3 delta =
            currentRevealPosition -
            lastBakedRevealPosition;


        // Reveal은 XZ만 사용
        delta.y = 0f;


        if (delta.sqrMagnitude >=
            revealUpdateDistance *
            revealUpdateDistance)
        {
            BakeReveal(
                currentRevealPosition,
                defaultRevealRadius
            );


            lastBakedRevealPosition =
                currentRevealPosition;


            UploadFogTexture();
        }
    }


    // ============================================================
    // RESOLVE PLAYER
    // ============================================================

    private void ResolvePlayerChild()
    {
        if (playerTransform != null)
            return;


        if (playerBody == null)
            return;


        // PlayerBody 바로 아래의 Player만 사용한다.
        //
        // GameObject.FindGameObjectWithTag("Player")
        // 를 사용하지 않는다.

        for (int i = 0;
             i < playerBody.childCount;
             i++)
        {
            Transform child =
                playerBody.GetChild(i);


            if (child.name == playerChildName)
            {
                playerTransform =
                    child;

                return;
            }
        }
    }


    // ============================================================
    // SET PLAYER
    // ============================================================

    public void SetPlayer(
        Transform player
    )
    {
        playerTransform =
            player;


        if (playerTransform == null)
            return;


        currentRevealPosition =
            playerTransform.position;


        lastBakedRevealPosition =
            currentRevealPosition;


        currentRevealRadius =
            defaultRevealRadius;


        // ★ SetPlayer는 Player 등록만 한다.
        //
        // Fog를 켜는 것은 Reveal()이다.

        ApplyShaderGlobals();
    }


    // ============================================================
    // REVEAL
    // ============================================================

    public void Reveal(
        Vector3 worldPosition
    )
    {
        Reveal(
            worldPosition,
            defaultRevealRadius
        );
    }


    public void Reveal(
        Vector3 worldPosition,
        float radius
    )
    {
        if (!isReady)
        {
            Debug.LogWarning(
                "[FogOfWar] 아직 초기화되지 않았습니다."
            );

            return;
        }


        radius =
            Mathf.Max(
                0f,
                radius
            );


        // Player 연결
        ResolvePlayerChild();


        // --------------------------------------------------------
        // Reveal 위치
        // --------------------------------------------------------

        currentRevealPosition =
            worldPosition;


        lastBakedRevealPosition =
            worldPosition;


        currentRevealRadius =
            radius;


        hasRevealPosition =
            true;


        // --------------------------------------------------------
        // ★ 여기서 처음 Fog ON
        // --------------------------------------------------------

        fogActive = true;


        // --------------------------------------------------------
        // 즉시 영구 Reveal
        // --------------------------------------------------------

        BakeReveal(
            worldPosition,
            radius
        );


        UploadFogTexture();


        ApplyShaderGlobals();
    }


    // ============================================================
    // REVEAL - TRANSFORM
    // ============================================================

    public void Reveal(
        Transform player
    )
    {
        if (player == null)
            return;


        playerTransform =
            player;


        Reveal(
            player.position,
            defaultRevealRadius
        );
    }


    // ============================================================
    // IS REVEALED
    // ============================================================

    public bool IsWorldPositionRevealed(
        Vector3 worldPosition,
        byte revealedThreshold = 8
    )
    {
        if (!isReady ||
            fogPixels == null)
        {
            return false;
        }


        Vector2 uv =
            WorldToNormalized(
                worldPosition
            );


        int x =
            Mathf.RoundToInt(
                uv.x *
                (textureResolution - 1)
            );


        int y =
            Mathf.RoundToInt(
                uv.y *
                (textureResolution - 1)
            );


        if (x < 0 ||
            x >= textureResolution ||
            y < 0 ||
            y >= textureResolution)
        {
            return false;
        }


        int index =
            y *
            textureResolution +
            x;


        return
            fogPixels[index].r >=
            revealedThreshold;
    }


    // ============================================================
    // BAKE REVEAL
    // ============================================================

    private void BakeReveal(
        Vector3 worldPosition,
        float radius
    )
    {
        if (fogPixels == null)
            return;


        // --------------------------------------------------------
        // World → UV
        // --------------------------------------------------------

        Vector2 uv =
            WorldToNormalized(
                worldPosition
            );


        int centerX =
            Mathf.RoundToInt(
                uv.x *
                (textureResolution - 1)
            );


        int centerY =
            Mathf.RoundToInt(
                uv.y *
                (textureResolution - 1)
            );


        // --------------------------------------------------------
        // Radius 0
        // --------------------------------------------------------

        if (radius <= 0f)
        {
            if (centerX >= 0 &&
                centerX < textureResolution &&
                centerY >= 0 &&
                centerY < textureResolution)
            {
                int index =
                    centerY *
                    textureResolution +
                    centerX;


                fogPixels[index] =
                    new Color32(
                        255,
                        255,
                        255,
                        255
                    );
            }


            return;
        }


        // --------------------------------------------------------
        // World → Pixel
        // --------------------------------------------------------

        float pixelsPerWorldX =
            (textureResolution - 1f) /
            mapWorldSize.x;


        float pixelsPerWorldZ =
            (textureResolution - 1f) /
            mapWorldSize.y;


        int radiusX =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    radius *
                    pixelsPerWorldX
                )
            );


        int radiusZ =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    radius *
                    pixelsPerWorldZ
                )
            );


        int minX =
            Mathf.Max(
                0,
                centerX -
                radiusX
            );


        int maxX =
            Mathf.Min(
                textureResolution - 1,
                centerX +
                radiusX
            );


        int minY =
            Mathf.Max(
                0,
                centerY -
                radiusZ
            );


        int maxY =
            Mathf.Min(
                textureResolution - 1,
                centerY +
                radiusZ
            );


        float softness =
            Mathf.Clamp01(
                edgeSoftness
            );


        // --------------------------------------------------------
        // Circle
        // --------------------------------------------------------

        for (int y = minY;
             y <= maxY;
             y++)
        {
            for (int x = minX;
                 x <= maxX;
                 x++)
            {
                float dx =
                    (x - centerX) /
                    (float)radiusX;


                float dy =
                    (y - centerY) /
                    (float)radiusZ;


                float distance =
                    Mathf.Sqrt(
                        dx * dx +
                        dy * dy
                    );


                if (distance > 1f)
                    continue;


                float reveal;


                if (softness <= 0f)
                {
                    reveal = 1f;
                }
                else
                {
                    reveal =
                        1f -
                        Mathf.SmoothStep(
                            1f - softness,
                            1f,
                            distance
                        );
                }


                int index =
                    y *
                    textureResolution +
                    x;


                byte value =
                    (byte)Mathf.RoundToInt(
                        reveal *
                        255f
                    );


                // ★ 영구 Reveal
                //
                // 한번 밝아진 곳은
                // 절대 다시 어두워지지 않는다.

                if (value >
                    fogPixels[index].r)
                {
                    fogPixels[index] =
                        new Color32(
                            value,
                            value,
                            value,
                            255
                        );
                }
            }
        }
    }


    // ============================================================
    // WORLD → NORMALIZED
    // ============================================================

    private Vector2 WorldToNormalized(
        Vector3 worldPosition
    )
    {
        float u =
            (
                worldPosition.x -
                mapWorldMin.x
            )
            /
            mapWorldSize.x;


        float v =
            (
                worldPosition.z -
                mapWorldMin.y
            )
            /
            mapWorldSize.y;


        return new Vector2(
            u,
            v
        );
    }


    // ============================================================
    // SHADER GLOBALS
    // ============================================================

    private void ApplyShaderGlobals()
    {
        if (!isReady ||
            fogTexture == null)
        {
            return;
        }


        // --------------------------------------------------------
        // Fog Texture
        // --------------------------------------------------------

        Shader.SetGlobalTexture(
            FogTextureID,
            fogTexture
        );


        // --------------------------------------------------------
        // Map 영역
        // --------------------------------------------------------

        Shader.SetGlobalVector(
            MapWorldMinAndSizeID,
            new Vector4(
                mapWorldMin.x,
                mapWorldMin.y,
                mapWorldSize.x,
                mapWorldSize.y
            )
        );


        // --------------------------------------------------------
        // Fog Opacity
        // --------------------------------------------------------

        Shader.SetGlobalFloat(
            FogOpacityID,
            fogOpacity
        );


        // --------------------------------------------------------
        // ★ Fog Active
        // --------------------------------------------------------

        Shader.SetGlobalFloat(
            FogActiveID,
            fogActive
                ? 1f
                : 0f
        );


        // --------------------------------------------------------
        // 현재 Player 위치
        // --------------------------------------------------------

        Vector3 position =
            hasRevealPosition
                ? currentRevealPosition
                : Vector3.zero;


        Shader.SetGlobalVector(
            RevealWorldPositionAndRadiusID,
            new Vector4(
                position.x,
                position.z,
                currentRevealRadius,
                0f
            )
        );
    }


    // ============================================================
    // CLEAR
    // ============================================================

    private void ClearFogPixels()
    {
        Color32 hidden =
            new Color32(
                0,
                0,
                0,
                255
            );


        for (int i = 0;
             i < fogPixels.Length;
             i++)
        {
            fogPixels[i] =
                hidden;
        }
    }


    // ============================================================
    // UPLOAD
    // ============================================================

    private void UploadFogTexture()
    {
        if (fogTexture == null ||
            fogPixels == null)
        {
            return;
        }


        fogTexture.SetPixels32(
            fogPixels
        );


        fogTexture.Apply(
            false,
            false
        );
    }


    // ============================================================
    // RESET
    // ============================================================

    public void ResetFog()
    {
        if (!isReady)
            return;


        ClearFogPixels();

        UploadFogTexture();


        // ★ Reset하면 Fog OFF
        //
        // 다시 Reveal()을 호출해야 한다.

        fogActive = false;

        hasRevealPosition = false;

        currentRevealPosition =
            Vector3.zero;

        lastBakedRevealPosition =
            Vector3.zero;

        currentRevealRadius =
            defaultRevealRadius;


        ApplyShaderGlobals();
    }


    // ============================================================
    // DESTROY
    // ============================================================

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;


        if (fogTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(
                    fogTexture
                );
            }
            else
            {
                DestroyImmediate(
                    fogTexture
                );
            }
        }


        fogTexture = null;
        fogPixels = null;
    }
}