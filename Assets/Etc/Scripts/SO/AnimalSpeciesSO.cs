using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public enum Stage2SetId
{
    None,
    Set1,
    Set2,
    Set3,
    Set4,
    Set5
}

public enum AnimalTopicProfile
{
    None,
    AbandonedPet,    // 유기/이별 중심
    FarmOverwork,    // 농장 노동, 과로 중심
    StreetLife,      // 거리 생활 중심
    AccidentVictim,  // 사고/부상 중심
    WildCapture,     // 야생 포획 중심
    LabAnimal        // 실험/병원 중심
}

#region Fixed profile enums (Species-level, NOT runtime)

/// <summary>
/// 동물의 "성격" (종 고정 데이터)
/// </summary>
public enum AnimalPersonality
{
    Calm,
    Curious,
    Proud,
    Shy,
    Aggressive,
    Gentle,
    Playful,
    Anxious
}

/// <summary>
/// 동물의 "기본 상태" (종 고정 데이터)
/// </summary>
public enum AnimalBaseStatus
{
    Healthy,
    Tired,
    Hungry,
    Injured,
    Overstimulated
}

#endregion

#region Deprecated enums (kept for backward compatibility)

/// <summary>
/// (Deprecated) 예전 구조에서 사용하던 트리거.
/// - 현재는 dislikes(회피/트리거)로 대체.
/// - 기존 에셋/코드가 깨지지 않도록 enum 자체는 유지.
/// </summary>
public enum AnimalTriggerType
{
    LoudNoise,
    SuddenApproach,
    DirectEyeContact,
    Touching,
    Cage,
    Water,
    MaleVoice,
    FlashLight,
    Crowd
}

/// <summary>
/// (Deprecated) 예전 구조에서 사용하던 선호 환경.
/// - 현재는 likes(선호)로 대체.
/// - 기존 에셋/코드가 깨지지 않도록 enum 자체는 유지.
/// </summary>
public enum PreferredEnvironment
{
    Cold,
    Warm,
    Quiet,
    Spacious,
    WaterNearby,
    HighPlace,
    Dark,
    Social,
    Solitary
}

#endregion

#region Preferences (Likes/Dislikes share the same categories)

/// <summary>
/// Likes / Dislikes가 공통으로 사용하는 카테고리
/// </summary>
public enum PreferenceCategory
{
    Environment,
    Food,
    Sound,
    Social,
    Interaction,
    Topic
}

/// <summary>
/// (Global) 예전 구조에서 사용하던 PreferenceItem.
/// - 현재는 AnimalSpeciesSO 내부 PreferenceItem을 사용.
/// - 기존 참조 호환용으로만 유지.
/// </summary>
[Serializable]
public class PreferenceItem
{
    public PreferenceCategory category;
    public string key;

    [Range(1, 3)]
    public int importance = 1;
}

#endregion

/// <summary>
/// AnimalSpeciesSO = "동물 종" 고정 데이터 (런타임 코어)
/// - 런타임에서는 아래 필드만 사용하도록 단순화.
/// - Stage2 프리셋 자동 채움/생성 로직은 Editor 전용 코드로 분리.
/// </summary>
[CreateAssetMenu(menuName = "Game/Animal/Species")]
public class AnimalSpeciesSO : ScriptableObject
{
    [Header("Stage2 분류(선택)")]
    public Stage2SetId stage2SetId = Stage2SetId.None;

    [Serializable]
    public class PreferenceItem
    {
        public PreferenceCategory category;
        public string key;
        public int importance = 1;
    }

    [Header("기본 정보")]
    public string id;

    // [수정] 플레이어에게 보여줄 이름은 LocalizedString으로 변경
    public LocalizedString localizedDisplayName;
    public string displayName => localizedDisplayName.GetLocalizedString();

    public Sprite portrait;

    [Header("Audio (Cries)")]
    public AudioClip crySound1; // 기본 울음소리
    public AudioClip crySound2; // 보조 울음소리 (선택 사항)

    [SerializeField] private RuntimeAnimatorController animatorController;
    public RuntimeAnimatorController AnimatorController => animatorController;

    [Header("Fixed Profile (Species-level)")]
    public AnimalPersonality personality;
    public AnimalBaseStatus baseStatus;

    [Header("Topic Profile (선택)")]
    public AnimalTopicProfile topicProfile = AnimalTopicProfile.None;

    [Header("Preferences (Fixed)")]
    public List<PreferenceItem> likes = new List<PreferenceItem>();
    public List<PreferenceItem> dislikes = new List<PreferenceItem>();

    // --------------------------------------------------------------------
    // Stage2: Trauma Profile (New)
    // - 플레이어에게는 노출되지 않도록 UI에서 사용 주의
    // - 판정은 문자열 키 기반으로 안정적으로 처리
    // --------------------------------------------------------------------

    [Header("Trauma Profile (Stage2)")]
    public LocalizedString loc_traumaSummaryForAI;
    public string traumaSummaryForAI => loc_traumaSummaryForAI.GetLocalizedString();

    [Tooltip("정답(원인) 키. 예: CAPTURE_RESTRAINT, ABUSE_HANDS, ACCIDENT_NOISE")]
    public string traumaAnswerKey;

    [Tooltip("플레이어가 '진단'으로 제출 가능한 후보(원인) 키 목록")]
    public List<string> traumaCandidateKeys = new List<string>();

    [Header("Clue Keys (Allowed)")]
    [Tooltip("원인 단서 키(과거 사건 유형). 예: CAPTURE, ABUSE, ACCIDENT")]
    public List<string> traumaCauseKeys = new List<string>();

    [Tooltip("트리거 단서 키(무엇이 자극인가). 예: LOUD_NOISE, HUMAN_HAND, CAGE")]
    public List<string> traumaTriggerKeys = new List<string>();

    [Tooltip("반응 단서 키(어떻게 반응하는가). 예: FREEZE, AGGRESSION, AVOID")]
    public List<string> traumaReactionKeys = new List<string>();

    [Tooltip("진정 단서 키(무엇이 도움이 되는가). 예: KEEP_DISTANCE, QUIET_SPACE")]
    public List<string> traumaSootheKeys = new List<string>();

    // Compatibility helpers (read-only)
    // Some scripts still expect so.EnvironmentKey / so.GetEnvironmentKey().

    public string EnvironmentKey => GetFirstLikeKey(PreferenceCategory.Environment);

    public string GetEnvironmentKey() => EnvironmentKey;

    private string GetFirstLikeKey(PreferenceCategory category)
    {
        if (likes == null) return string.Empty;

        for (int i = 0; i < likes.Count; i++)
        {
            var item = likes[i];
            if (item == null) continue;
            if (item.category != category) continue;
            if (string.IsNullOrWhiteSpace(item.key)) continue;
            return item.key.Trim();
        }

        return string.Empty;
    }

    // 랜덤하게 소리를 가져오는 헬퍼 함수 (선택 사항)
    public AudioClip GetRandomCry()
    {
        if (crySound1 != null && crySound2 != null)
            return UnityEngine.Random.value > 0.5f ? crySound1 : crySound2;

        return crySound1 ?? crySound2; // 둘 중 하나라도 있으면 반환
    }

    // --------------------------------------------------------------------
    // Legacy fields (사용하지 않지만, 기존 에셋 직렬화 호환을 위해 유지)
    // --------------------------------------------------------------------

    [Header("Legacy (do not use)")]
    [SerializeField, HideInInspector] private int difficulty = 1;

    [SerializeField, HideInInspector]
    [TextArea(2, 4)]
    private string shortBio;

    [SerializeField, HideInInspector] private AnimalTriggerType trigger;
    [SerializeField, HideInInspector] private PreferredEnvironment preferredEnvironment;

    public static string BuildTopicKey(PreferenceItem item)
    {
        if (item == null) return string.Empty;
        return item.category + ":" + item.key;
    }
}