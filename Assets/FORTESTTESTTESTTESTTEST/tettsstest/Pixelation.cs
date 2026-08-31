using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class Pixelation : MonoBehaviour
{
    [SerializeField]
    private Shader shader;

    [SerializeField]
    [Range(1, 32)]
    private float pixelSize = 4f;

    private Material material;

    private void OnEnable()
    {
        CreateMaterial();
    }

    private void OnValidate()
    {
        CreateMaterial();

        if (material != null)
        {
            material.SetFloat("_PixelSize", pixelSize);
        }
    }

    private void OnDisable()
    {
        if (material != null)
        {
            DestroyImmediate(material);
            material = null;
        }
    }

    private void CreateMaterial()
    {
        if (material != null)
            return;

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Pixelation");
        }

        if (shader == null)
        {
            Debug.LogError(
                "[Pixelation] Hidden/Pixelation Shader를 찾을 수 없습니다."
            );

            return;
        }

        material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
    }

    private void OnRenderImage(
        RenderTexture source,
        RenderTexture destination)
    {
        Debug.Log("PIXELATION OnRenderImage 호출됨");

        CreateMaterial();

        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetFloat("_PixelSize", pixelSize);

        Graphics.Blit(source, destination, material);
    }
}