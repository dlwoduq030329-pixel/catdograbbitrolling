using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class OptionSaveData
{
    public string optionId;
    public float value;
}

[System.Serializable]
public class GraphicsSettingsSaveData
{
    public List<OptionSaveData> options = new();
    public List<EnumSaveData> enumOptions = new();
}
[Serializable]
public class EnumSaveData
{
    public string key;
    public int value;
}

