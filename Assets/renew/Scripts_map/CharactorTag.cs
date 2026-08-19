using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharactorTag : MonoBehaviour
{
    [SerializeField]
    Renderer[] renderers;
    [SerializeField]
    Color idleColor;

    public void SetTag()
    {
        for(int i =0;i<renderers.Length;i++)
        {
            Material mat = renderers[i].material;
            int propertyID = Shader.PropertyToID("_Outline_Color");

            if (mat.HasProperty(propertyID))
            {
                mat.SetColor(propertyID, new Color(1,0,0));
                mat.SetFloat("_Outline_Width", 2132 * 3);
            }else
            {
                for (int j = 0; j < mat.shader.GetPropertyCount(); j++)
                {
                    string propertyName = mat.shader.GetPropertyName(j);

                    if (propertyName.ToLower().Contains("outline"))
                    {
                        Debug.Log(propertyName);
                    }
                }
            }
        }
    }

    public void SetIdle()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].material;
            int propertyID = Shader.PropertyToID("_Outline_Color");

            if (mat.HasProperty(propertyID))
            {
                mat.SetColor(propertyID, idleColor);
                mat.SetFloat("_Outline_Width", 2132);

            }
            else
            {
                Debug.Log("프로퍼티 ID 없음!");
            }
        }

    }
}
