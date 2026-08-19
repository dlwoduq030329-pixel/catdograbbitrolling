using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class battleUISystem : MonoBehaviour
{
    [SerializeField]
    Slider apBar;
    [SerializeField]
    TextMeshProUGUI apText;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateAp(float sliderValue,string howAP)
    {
        apBar.value = sliderValue;
        apText.text = howAP;
    }
}
