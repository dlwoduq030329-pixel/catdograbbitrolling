using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class diceResultUI : MonoBehaviour
{
    [SerializeField]
    Sprite[] diceSP;

    public void Init(int x)
    {
        GetComponent<Image>().sprite = diceSP[x-1];
    }

    public void OnEnable()
    {
        
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
