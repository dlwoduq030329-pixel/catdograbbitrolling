using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class rgbSliderSet : MonoBehaviour
{
    [SerializeField]
    Vector3 zeroColor;
    [SerializeField]
    Vector3 oneColor;
    [SerializeField]
    Image fill;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ColorChange(float temp)
    {
        float colorX = Mathf.Lerp(zeroColor.x, oneColor.x, temp);
        float colorY = Mathf.Lerp(zeroColor.y, oneColor.y, temp);
        float colorZ = Mathf.Lerp(zeroColor.z, oneColor.z, temp);

        fill.color = new Color(colorX, colorY, colorZ);

    }
}
