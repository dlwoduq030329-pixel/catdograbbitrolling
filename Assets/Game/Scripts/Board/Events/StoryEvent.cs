using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Success
{
    money,
    weapon,
    card,
    status
}

public enum Fail
{
    money,
    battle,

}

[System.Serializable]
public class StoryEvent
{
    [TextArea]
    public string story;
    [Header("str - 0 | wis - 1 | dex - 2 | vit = 3|")]
    public int[] needStatus;
    public Sprite storyIMG;
    [Header("str - 0 | wis - 1 | dex - 2 | vit = 3|")]
    public int needStateIndex;
    [Header("성공 보상")]
    public Success success;
    [Header("실패 보상")]
    public Fail fail;
    [Header("성공 보상 수")]
    public int successCount;
    [Header("실패 보상 수")]
    public int failCount;
    [Header("성공 보상 스테이터스 index")]
    public int getSTIndex;
    [Header("실패 전투 적 index")]
    public int battleenemyIndex;



}
