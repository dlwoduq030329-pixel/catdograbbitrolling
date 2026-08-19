using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayMenuSelect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("Hover 시 활성화할 자식 오브젝트들")]
    public GameObject[] hoverObjects; // 자식 3개

    public GameObject nameTag;
    //public GameObject option;

    public enum MenuType
    {
        Single,
        Multi,
        CardList,
        Exit
    }

    public MenuType menuType;

    void Start()
    {
        SetHover(false);
    }

    void SetHover(bool isOn)
    {
        foreach (var obj in hoverObjects)
        {
            obj.SetActive(isOn);
        }
    }

    // 마우스 올렸을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHover(true);
    }

    // 마우스 나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false);
    }

    // 클릭했을 때
    public void OnPointerClick(PointerEventData eventData)
    {
        Execute();
    }

    void Execute()
    {
        switch (menuType)
        {
            case MenuType.Single:
                Debug.Log("싱글 플레이 실행");
                nameTag.SetActive(true);
                break;

            case MenuType.Multi:
                Debug.Log("멀티 플레이 실행");
                break;

            case MenuType.CardList:
                Debug.Log("카드 도감 실행");
                //option.SetActive(true);
                break;

            case MenuType.Exit:
                break;
        }
    }
}

