using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>선택 가능한 캐릭터들의 기본 데이터를 한곳에 보관합니다.</summary>
[CreateAssetMenu(fileName = "PlayerCharacterData", menuName = "Raptorz/Player Character Data")]
public sealed class PlayerCharacterData : ScriptableObject
{
    [Serializable]
    public sealed class Character
    {
        [Header("캐릭터 정보")]
        public string characterName;
        public string title;
        [TextArea(3, 8)] public string description;
        public Sprite selectImage;
        public GameObject battlePrefab;

        [Header("기본 능력치")]
        [Min(0)] public int strength;
        [Min(0)] public int dexterity;
        [Min(0)] public int intelligence;
        [Min(0)] public int wisdom;
        [Min(0)] public int charisma;
        [Min(0)] public int vitality;

        [Header("기본 전투 자원")]
        [Min(0)] public int baseAP = 6;
        [Min(0)] public int baseMP = 6;
        [Min(1)] public int baseHP = 15;
    }

    [SerializeField] private List<Character> characters = new List<Character>();
    public IReadOnlyList<Character> Characters => characters;
}
