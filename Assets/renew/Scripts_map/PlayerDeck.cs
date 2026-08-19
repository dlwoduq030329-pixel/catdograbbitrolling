using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDeck : MonoBehaviour
{
    public Dictionary<int, int> cardPool = new Dictionary<int, int>();
    public int[] deckCardforUI = new int[10];
    public List<int> deckCard = new List<int>();
    public List<int> handCard = new List<int>();

    public void UICardInit()
    {
        for (int i = 0; i < deckCardforUI.Length; i++)
        {
            deckCardforUI[i] = -1;
        }
    }

    public void AddCardPool(int cardIndex, int cardCount)
    {
        if (cardPool.ContainsKey(cardIndex))
        {
            int count = cardPool[cardIndex];
            if (count >= 2) return;

        }
        else
        {
            cardPool.Add(cardIndex, cardCount);
        }

        int changeCardIndex = -1;

        for (int i = 0; i < deckCardforUI.Length; i++)
        {
            if (deckCardforUI[i] == -1)
            {
                changeCardIndex = i;
                break;
            }
        }

        if (changeCardIndex == -1) return;
        for (int j = changeCardIndex; j < changeCardIndex + cardCount; j++)
        {
            deckCardforUI[j] = cardIndex;
        }

    }



    public void ChangeCard(int listIndex, int changecardIndex)
    {
        deckCardforUI[listIndex] = changecardIndex;
    }

    public void suffleDeck()
    {
        for (int i = 0; i < deckCard.Count; i++)
        {
            int randomIndex = Random.Range(0, deckCard.Count);
            int temp = deckCard[randomIndex];
            deckCard[randomIndex] = deckCard[i];
            deckCard[i] = temp;
        }
    }

    public void battleCardInit()
    {
        deckCard.Clear();

        foreach (var temp in deckCardforUI)
        {
            deckCard.Add(temp);
        }

        suffleDeck();
    }

    public void DrawCard()
    {
        for (int i = 0; i < 5; i++)
        {
            int temp = Random.Range(0, deckCard.Count);
            handCard.Add(temp);
            deckCard.Remove(temp);
        }
    }

    public void UseCard(int x)
    {
        handCard.Remove(x);
        deckCard.Add(x);
    }

}