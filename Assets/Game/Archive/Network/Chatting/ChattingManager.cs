using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class ChattingManager : MonoBehaviourPun
{
    [SerializeField]
    GameObject chaText;

    GameObject acChat;
    GameObject deChat;

    TMP_InputField chatField;
    TextMeshProUGUI lateChat;

    Transform contentTF;

    ScrollRect sliderForChat;
    string tempChat;

    bool ac = false;
    PhotonView pv;
    // Start is called before the first frame update
    void Start()
    {
        pv = GetComponent<PhotonView>();
    }

    // Update is called once per frame
    void Update()
    {
        /*     if(chatField.text == string.Empty)
                     {
                         chatField.DeactivateInputField();
                         EventSystem.current.SetSelectedGameObject(null);
                         ChatSwap();
                         resetSwap  = false;
                         return;
                     }*/

        if (Input.GetKeyDown(KeyCode.Return))
        {

            if(!ac)
            {
                AcChat();
            }else
            {
                SendChat();
            }
        }

        if (Input.GetMouseButtonDown(0) && ac)
        { 
            if (!EventSystem.current.IsPointerOverGameObject()) 
            {
                DeChat(); 
            } 
        }

    }

    public void AcChat()
    {
        chatField.Select();
        chatField.ActivateInputField();

        acChat.SetActive(true);
        deChat.SetActive(false);

        ac = true;
    }

    public void DeChat()
    {
        chatField.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(null);

        acChat.SetActive(false);
        deChat.SetActive(true);

        ac = false;
    }
    public void SendChat()
    {
        if(chatField.text == "")
        {
            DeChat();
        }else
        {
            tempChat = chatField.text;
            pv.RPC("RPCSendChat", RpcTarget.All,tempChat);
            tempChat = null;
            chatField.text = null;

            chatField.Select();
            chatField.ActivateInputField();

        }
    }

    [PunRPC]
    public void RPCSendChat(string text)
    {
        var temp = Instantiate(chaText, Vector3.zero, Quaternion.identity);
        lateChat.text = tempChat;
        temp.transform.SetParent(contentTF, false);
        TextMeshProUGUI x = temp.GetComponent<TextMeshProUGUI>();
        x.text = tempChat;

        Canvas.ForceUpdateCanvases();
        sliderForChat.verticalNormalizedPosition = 0;
    }

    

    public void ConnectContent(GameObject ac, GameObject de, TMP_InputField chat, TextMeshProUGUI late, Transform tf, ScrollRect sl)
    {
        acChat = ac;
        deChat = de;
        chatField = chat;
        lateChat = late;
        contentTF = tf;
        sliderForChat = sl;
    }


}
