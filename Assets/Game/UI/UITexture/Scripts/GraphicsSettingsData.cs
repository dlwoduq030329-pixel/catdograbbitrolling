using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "Settings/Graphics Settings")]
public class GraphicsSettingsData : ScriptableObject
{
    [Range(0.6f, 1.4f)]
    public float brightness = 1f;
}