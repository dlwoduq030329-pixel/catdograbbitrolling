using System;
/*using System.Collections;
using System.Collections.Generic;*/
using UnityEngine;

public enum OptionType
{
    Slider,
    Dropdown,
    ButtonGroup
}

[Serializable]
public class GraphicsOption
{
    public string optionId;        // "Brightness", "Resolution" µî
    public string displayName;
    public OptionType optionType;

    // Slider
    public float minValue;
    public float maxValue;
    public float defaultValue;

    // Dropdown / ButtonGroup
    public string[] options;
    public int defaultIndex;
}
