using UnityEngine;

public enum ProfileRevealMode
{
    All,
    Discover
}

[CreateAssetMenu(menuName = "ShelterTalk/Stage Config", fileName = "StageConfigSO")]
public class StageConfigSO : ScriptableObject
{
    [Header("스테이지 기본 정보")]
    [Tooltip("스테이지 번호입니다. 예: 1, 2, 3")]
    public int stageId = 1;

    [Header("런 규칙")]
    [Tooltip("한 번의 런이 며칠로 구성되는지 설정합니다. 기본 7일")]
    public int daysPerRun = 7;

    [Tooltip("런 성공을 위해 '성공한 동물 세션 수'가 최소 몇 마리 이상이어야 하는지 설정합니다.")]
    public int animalsToWinRun = 3;

    [Header("동물 세션 규칙")]
    [Tooltip("동물 1마리와 대화할 수 있는 최대 턴 수입니다. 예: Stage1=5, Stage2=7")]
    public int turnsPerAnimal = 5;

    [Tooltip("동물 세션 성공을 위해 필요한 호감(heartOpen) 횟수입니다. 예: Stage1=3, Stage2=4")]
    public int heartsToWinAnimal = 3;

    [Header("추가 정보(선택)")]
    [Tooltip("해당 스테이지에서 등장 가능한 전체 동물 종 수입니다. Day1에는 즉시 사용하지 않아도 됩니다.")]
    public int totalSpeciesCount = 10;

    [Header("프로필 표시 모드")]
    [Tooltip("프로필 공개 방식입니다. All=전체 공개(튜토리얼), Discover=발견/도감 방식(추후 확장)")]
    [SerializeField] private ProfileRevealMode profileRevealMode = ProfileRevealMode.All;

    public ProfileRevealMode ProfileRevealMode => profileRevealMode;
}