using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class statusinfoSet : MonoBehaviour
{
    [SerializeField]
    string[] statusString;
    [SerializeField]
    TextMeshProUGUI infoText;
    Canvas canvas;
    RectTransform rect;

    private void Start()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

    }

    public void SetInfo(int x)
    {
        infoText.text = statusString[x];
    }

    private void Update()
    {
        RectTransform parent = rect.parent as RectTransform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            Input.mousePosition,
            null,
            out Vector2 pos))
        {
            rect.anchoredPosition = pos;
        }
    }
}
