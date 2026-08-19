using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class chattingUIPack : MonoBehaviour
{
    [SerializeField]
    GameObject activedChat;
    [SerializeField]
    GameObject deactiveChat;

    [SerializeField]
    TMP_InputField myChatActive;
    [SerializeField]
    TextMeshProUGUI recentChat;

    [SerializeField]
    Transform content;

    [SerializeField]
    ScrollRect chatSlider;

    // Start is called before the first frame update
    void Start()
    {
       var temp = PhotonNetwork.Instantiate("ChattingManager", Vector3.zero, Quaternion.identity);
       ChattingManager cm = temp.GetComponent<ChattingManager>();
        cm.ConnectContent(activedChat,deactiveChat,myChatActive,recentChat,content,chatSlider);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
