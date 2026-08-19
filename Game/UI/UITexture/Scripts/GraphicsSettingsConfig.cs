//using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Config/Graphics Settings", fileName = "GraphicsSettingsConfig")]
public class GraphicsSettingsConfig : ScriptableObject
{
    public List<GraphicsOption> options;
    public List<EnumOption> enumOptions;     // 시스템 정의
}
