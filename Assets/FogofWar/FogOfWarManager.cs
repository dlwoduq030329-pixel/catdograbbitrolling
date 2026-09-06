using UnityEngine;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    // ============================================================
    // MAP
    // ============================================================

    [Header("Map")]
    [SerializeField]
    private NewMapGenerator mapGenerator;


    // ============================================================
    // PLAYER
    // ============================================================

    [Header("Player")]
    [SerializeField]
    private Transform playerTransform;

    [SerializeField]
    private bool autoFindPlayerByTag = true;

    [SerializeField]
    private string playerTag = "Player";


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
    [SerializeField]
    private bool revealAutomatically = true;

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

    // PlayerMap 실제 외곽 영역
    private Vector2 mapWorldMin;
    private Vector2 mapWorldSize;

    // 현재 플레이어 위치
    private Vector3 currentRevealPosition;

    // 마지막으로 Texture에 굽은 위치
    private Vector3 lastBakedRevealPosition;

    // 현재 Reveal 반경
    private float currentRevealRadius;

    private bool hasRevealPosition;
    private bool isReady;


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
        if (mapGenerator == null)
        {
            mapGenerator =
                FindObjectOfType<NewMapGenerator>();
        }


        if (playerTransform == null &&
            autoFindPlayerByTag)
        {
            try
            {
                GameObject player =
                    GameObject.FindGameObjectWithTag(
                        playerTag
                    );

                if (player != null)
                {
                    playerTransform =
                        player.transform;
                }
            }
            catch (UnityException)
            {
                // Player Tag가 없으면
                // Inspector에서 직접 지정하면 됩니다.
            }
        }


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
                "[FogOfWar] NewMapGenerator를 찾을 수 없습니다."
            );

            return;
        }


        // --------------------------------------------------------
        // 맵 생성이 끝날 때까지 기다림
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
        // NewMapGenerator 정보
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
        // NewMapGenerator의 mapWorldOffset과 동일한 계산
        // --------------------------------------------------------

        float halfX =
            (mapSizeX - 1) *
            blockDistance *
            0.5f;

        float halfZ =
            (mapSizeZ - 1) *
            blockDistance *
            0.5f;


        // --------------------------------------------------------
        // 실제 PlayerMap 외곽
        // --------------------------------------------------------

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

        fogTexture.anisoLevel =
            0;


        fogPixels =
            new Color32[
                textureResolution *
                textureResolution
            ];


        // --------------------------------------------------------
        // 처음에는 전체 미공개
        // --------------------------------------------------------

        ClearFogPixels();

        UploadFogTexture();


        isReady = true;


        // --------------------------------------------------------
        // Player가 있으면 최초 위치 설정
        // --------------------------------------------------------

        if (playerTransform != null)
        {
            currentRevealPosition =
                playerTransform.position;

            lastBakedRevealPosition =
                currentRevealPosition;

            currentRevealRadius =
                defaultRevealRadius;

            hasRevealPosition = true;


            // 시작 위치 영구 Reveal
            BakeReveal(
                currentRevealPosition,
                currentRevealRadius
            );

            UploadFogTexture();
        }
        else
        {
            currentRevealPosition =
                Vector3.zero;

            currentRevealRadius =
                defaultRevealRadius;

            hasRevealPosition =
                false;
        }


        ApplyShaderGlobals();


        Debug.Log(
            "[FogOfWar] Initialized\n" +
            $"Map = {mapSizeX} x {mapSizeZ}\n" +
            $"BlockDistance = {blockDistance}\n" +
            $"WorldMin = {mapWorldMin}\n" +
            $"WorldSize = {mapWorldSize}"
        );
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (!isReady)
            return;


        if (playerTransform == null)
            return;


        // --------------------------------------------------------
        // 현재 플레이어 위치
        // --------------------------------------------------------

        currentRevealPosition =
            playerTransform.position;


        // --------------------------------------------------------
        // ★ Shader에는 매 프레임 현재 위치 전달
        //
        // 카메라 이동과 관계없이
        // Reveal 중심은 실제 플레이어 월드 위치를 따라감
        // --------------------------------------------------------

        ApplyShaderGlobals();


        if (!revealAutomatically)
            return;


        // --------------------------------------------------------
        // 플레이어가 일정 거리 이동했을 때
        // 영구 Reveal Texture에 굽기
        // --------------------------------------------------------

        Vector3 delta =
            currentRevealPosition -
            lastBakedRevealPosition;

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

        hasRevealPosition =
            true;


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


        currentRevealPosition =
            worldPosition;

        lastBakedRevealPosition =
            worldPosition;

        currentRevealRadius =
            radius;

        hasRevealPosition =
            true;


        // 영구 Reveal
        BakeReveal(
            worldPosition,
            radius
        );


        UploadFogTexture();

        ApplyShaderGlobals();
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


                // ------------------------------------------------
                // 이미 밝은 곳은 유지
                // ------------------------------------------------

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
            return;


        // --------------------------------------------------------
        // Fog Texture
        // --------------------------------------------------------

        Shader.SetGlobalTexture(
            FogTextureID,
            fogTexture
        );


        // --------------------------------------------------------
        // PlayerMap World 영역
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
        // 현재 플레이어 위치 + 반경
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
            return;


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


        if (playerTransform != null)
        {
            currentRevealPosition =
                playerTransform.position;

            lastBakedRevealPosition =
                currentRevealPosition;

            currentRevealRadius =
                defaultRevealRadius;

            hasRevealPosition =
                true;


            BakeReveal(
                currentRevealPosition,
                currentRevealRadius
            );


            UploadFogTexture();
        }


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