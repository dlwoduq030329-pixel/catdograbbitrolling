using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class cardTag : MonoBehaviour
{
    Image myIMG;
    int index;
    Button thisBTN;
    [SerializeField]
    Sprite[] statusSP;
    [SerializeField]
    GameObject stText;
    public void Init(RewardSt st)
    {
        myIMG = GetComponent<Image>();
        thisBTN = GetComponent<Button>();
        stText.SetActive(false);

        switch (st)
        {

            case RewardSt.equipment:
                {
                    //장비 중에서 선택 현재는 장비 기획x
                }
                break;
            case RewardSt.card:
                {
                    int temp = Random.Range(0, DataPool.Instance.cardDatabase.cards.Count);
                    int tryCount = 0;
                    int maxTry = 100;

                    do
                    {
                        temp++;
                        if (temp > DataPool.Instance.cardDatabase.cards.Count - 1) temp = 0;
                        tryCount++;

                        Debug.Log("cardIndex : " + temp);
                        if (tryCount > maxTry)
                        {
                            Debug.LogWarning("조건 만족 카드 없음");
                            return;
                        }

                    } while (!CheckCard(temp));
                    index = temp;
                    Debug.Log(myIMG.name);
                    myIMG.sprite = DataPool.Instance.cardDatabase.cards[index].myCardSprite;
                    thisBTN.onClick.RemoveAllListeners();
                    thisBTN.onClick.AddListener(GetCard);
                }
                break;
            case RewardSt.status:
                {
                    int targetStatus = Random.Range(0, 4);
                    int getStatus = Random.Range(1, 4);
                    myIMG.sprite = statusSP[targetStatus];
                    stText.SetActive(true);
                    stText.GetComponent<TextMeshProUGUI>().text = getStatus.ToString();
                    thisBTN.onClick.AddListener(() => GetStatus(targetStatus, getStatus));
                }
                break;

        }
        
    }

    public void GetEquip(int equipIndex)
    {
        
    }

    public bool CheckCard(int x)
    {
        bool temp = true;
        if (DataConfig.CardsCount.ContainsKey(x))
        {
            Debug.Log(x + "index값 카드 보유중");
            if (DataConfig.CardsCount[x] < 2)
            {
                temp = true;
            }
            else
            {
                temp = false;
            }
        }
        else
        {
            temp = true;
        }

        return temp;
    }

    public void LoadMainScene()
    {
        SceneManager.LoadScene(1);
    }

    public void GetCard()
    {
        DataConfig.AddDic(index,1);
        DataConfig.playerMoney += (DataConfig.hard * 10) * DataConfig.count;
        Time.timeScale = 1;
        LoadMainScene();

    }

    public void GetStatus(int targetindex,int amount)
    {
        DataConfig.playerDatas[targetindex] += amount;
        DataConfig.playerMoney += (DataConfig.hard * 10) * DataConfig.count;
        Time.timeScale = 1;

        LoadMainScene();
    }

}
