using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(
    fileName = "StoryDatabase",
    menuName = "Story/StoryDatabase"
    )]
public class StoryData : ScriptableObject
{ 
    [SerializeField]
    public List<StoryEvent> stories = new List<StoryEvent>();
}
