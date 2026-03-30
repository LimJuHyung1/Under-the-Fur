using System;
using System.Collections.Generic;
using UnityEngine; // [추가] Header 등을 사용하기 위함

[Serializable]
public class SaveData
{
    // Stage1을 1회 클리어하면 true (Stage1 재플레이 금지에 사용)
    public bool stage1Completed = false;

    // Stage1 10종 클리어로 Stage2 해금
    public bool stage2Unlocked = false;

    // (추후 확장) Stage3 해금
    public bool stage3Unlocked = false;

    // JsonUtility는 HashSet 직렬화가 불편해서 List로 저장
    public List<string> stage1ClearedSpeciesIds = new List<string>();

    // Stage2에서 호감 달성한 종들
    public List<string> stage2ClearedSpeciesIds = new List<string>();

    // (선택) UI/씬 시작 기본값에 도움
    public int lastSelectedStageId = 1;

    public List<int> savedRunDayResults = new List<int>();

    // --- [새로 추가된 필드] 중단하기 및 이어하기용 ---
    [Header("Run Progress (런 진행 정보)")]
    public bool hasSavedRun = false;             // 현재 중단된 '런' 데이터가 있는지 여부
    public int savedStageId = 0;                 // 중단된 런의 스테이지 ID
    public int savedDayIndex = 0;                // 며칠차인지 (0~6)
    public int savedRunSuccessCount = 0;         // 현재까지 구조 성공한 횟수
    public List<string> savedRunOrderIds = new List<string>(); // 셔플되었던 동물의 ID 순서 리스트
}


[Serializable]
public class SpeciesDiscoverSaveData
{
    public List<string> likes;
    public List<string> dislikes;
}
