using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class statusIndex : MonoBehaviour,IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    int statusindex;
    [SerializeField]
    statusinfoSet statusinfo;
    [SerializeField]
    GameObject infoobject;

    public void OnPointerEnter(PointerEventData eventData)
    {
        statusinfo.SetInfo(statusindex);
        infoobject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        infoobject.SetActive(false);
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
