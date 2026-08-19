using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI;

public class LinkHP : MonoBehaviour
{

    [SerializeField]
    Renderer rend;
    //[SerializeField]
    Material mat;
    [SerializeField]
    bool isBoos = false;

    private void Start()
    {
        mat = rend.material;

    }

    // Update is called once per frame
    void Update()
    {
        if (isBoos) return;
        this.gameObject.transform.LookAt(-Camera.main.transform.position);
    }

    public void UpdateHP(float now, float max)
    {
        // hpBar.ProgressBar.Value = now;
        float hpShow = now / max;   
        mat.SetFloat("_ProgressBar",hpShow);
    }
}
