using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PressKey : MonoBehaviour
{
    [SerializeField]
    Button keyBTN;
    [SerializeField]
    GameObject temp;
    [SerializeField]
    GameObject bgmOBJ;
    bool click = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }



    // Update is called once per frame
    void Update()
    {
        if (temp.activeSelf || click) return;
        
        if(Input.anyKeyDown)
        {
            click = true;
            keyBTN.onClick.Invoke();
            bgmOBJ.SetActive(true);
        }
    }
}
