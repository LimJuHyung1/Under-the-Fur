using System;
using System.Collections.Generic;
using UnityEngine;

#region Preferences 공통 정의 (Stage2 파일에 이미 있다면 중복 정의하지 말 것)

[Serializable]
public class PreferenceItemGlobal
{
    [Tooltip("선호/비선호의 큰 분류")]
    public PreferenceCategory category;

    [Tooltip("category에 따른 세부 키")]
    public string key;

    [Range(1, 3)]
    [Tooltip("1=보조, 2=중요, 3=핵심 정도의 느낌")]
    public int importance = 1;
}

#endregion

/// <summary>
/// Stage1 튜토리얼용 동물 종 데이터
/// - Stage2의 복잡한 Set 개념 없이, 기본적인 Likes/Dislikes만 자동 세팅.
/// - Environment / Food / Social / Interaction / Sound 축만 사용.
/// </summary>
[CreateAssetMenu(menuName = "Game/Animal/Stage1Species")]
public class Stage1AnimalSpeciesSO : ScriptableObject
{
    [Header("기본 정보")]
    public string id;
    public int difficulty = 1;
    public string displayName;
    public Sprite portrait;
    public Sprite background;

    [TextArea(2, 4)]
    [Tooltip("현장에서 관찰된 이 동물의 짧은 메모(현재 상태 요약)")]
    public string shortBio;

    [SerializeField] private RuntimeAnimatorController animatorController;
    public RuntimeAnimatorController AnimatorController => animatorController;

    [Header("고정 성향")]
    public AnimalPersonality personality = AnimalPersonality.Calm;
    public AnimalBaseStatus baseStatus = AnimalBaseStatus.Healthy;

    [Header("Preferences (Stage1 튜토리얼용)")]
    public List<PreferenceItem> likes = new List<PreferenceItem>();
    public List<PreferenceItem> dislikes = new List<PreferenceItem>();

    [Serializable]
    public class PreferenceItem
    {
        public PreferenceCategory category;
        public string key;
        public int importance = 1;
    }

    // 여기서부터는 Stage2용 AnimalSpeciesSO에 이미 존재하던 헬퍼들을
    // 그대로 복사하거나, 공용 헬퍼 클래스로 분리해서 재사용해도 된다.

    /// <summary>
    /// displayName이 비어 있으면 id를 사용하고, 둘 다 비면 "forest"로 처리.
    /// </summary>
    private string GetBaseName()
    {
        var name = !string.IsNullOrEmpty(displayName) ? id : displayName;
        if (string.IsNullOrEmpty(name))
            return "forest";

        return name.ToLowerInvariant().Trim();
    }

    private string GetEnvironmentKeyForThisSpecies()
    {
        var name = GetBaseName();

        switch (name)
        {
            // 사막/건조
            case "camel":
            case "oryx":
                return "desert";

            // 사바나/아프리카 초원
            case "lion":
            case "lioness":
            case "giraffe":
            case "zebra":
            case "antilope":
            case "hayena":
                return "savanna";

            // 초원/목초지/농장
            case "cow":
            case "cow brown":
            case "bull":
            case "bison":
            case "yak":
            case "jak":
            case "lama":
            case "alpacha":
            case "sheep":
            case "ram":
            case "work horse":
            case "brown horse":
            case "arabian horse":
            case "donkey":
            case "pig":
            case "chicken":
            case "water buffalo":
                return "grassland";

            // 숲/정글/산악
            case "brown bear":
            case "grizzly":
            case "grey wolf":
            case "greywolf":
            case "elite wolf":
            case "wolf":
            case "tiger":
            case "white tiger":
            case "jaguar":
            case "deer":
            case "stag":
            case "monkey":
            case "chimp":
                return "forest";

            // 늪/강/물가
            case "crocodile":
                return "swamp";
            case "hippo":
            case "turtle":
                return "river_lake";

            // 극지/눈
            case "polar bear":
            case "pinguin":
            case "penguin":
            case "mammoth":
            case "husky":
                return "arctic_snow";

            // 도시/주택가/실내
            case "cat black":
            case "cat large":
            case "cat orange":
            case "cat white":
            case "golden retriever":
            case "shepherd dog":
                return "urban";

            default:
                return "forest";
        }
    }

    private string GetFoodKeyForThisSpecies()
    {
        var name = GetBaseName();

        switch (name)
        {
            // 초식 위주
            case "antilope":
            case "bison":
            case "deer":
            case "giraffe":
            case "yak":
            case "jak":
            case "lama":
            case "alpacha":
            case "sheep":
            case "ram":
            case "cow":
            case "cow brown":
            case "water buffalo":
            case "horse":
            case "brown horse":
            case "arabian horse":
            case "work horse":
            case "donkey":
            case "camel":
                return "grass";

            // 잡식(농장/가축)
            case "pig":
            case "chicken":
                return "mix";

            // 개/늑대 계열
            case "golden retriever":
            case "husky":
            case "shepherd dog":
            case "grey wolf":
            case "greywolf":
            case "elite wolf":
            case "wolf":
                return "meat";

            // 고양이
            case "cat black":
            case "cat large":
            case "cat orange":
            case "cat white":
                return "meat";

            // 대형 포식자
            case "lion":
            case "lioness":
            case "tiger":
            case "white tiger":
            case "jaguar":
            case "brown bear":
            case "grizzly":
            case "polar bear":
                return "meat";

            // 초식 대형 동물
            case "hippo":
            case "rhino":
            case "rhino female":
            case "elephant":
            case "elephant female":
                return "grass";

            // 수중/반수생
            case "pinguin":
            case "penguin":
                return "fish";

            default:
                return "mix";
        }
    }

    private string GetSoundDislikeKeyForThisSpecies()
    {
        var name = GetBaseName();

        switch (name)
        {
            // 가축/초식 위주: 큰 소리 전반 싫어함
            case "alpacha":
            case "antilope":
            case "arabian horse":
            case "bison":
            case "brown horse":
            case "bull":
            case "camel":
            case "cow":
            case "cow brown":
            case "deer":
            case "donkey":
            case "elephant":
            case "elephant female":
            case "giraffe":
            case "hippo":
            case "jak":
            case "yak":
            case "lama":
            case "mammoth":
            case "oryx":
            case "ostrich":
            case "ram":
            case "rhino":
            case "rhino female":
            case "sheep":
            case "stag":
            case "water buffalo":
            case "work horse":
            case "zebra":
                return "loud";

            // 도시 생활 동물: 엔진/기계 소리에 민감
            case "cat black":
            case "cat large":
            case "cat orange":
            case "cat white":
            case "golden retriever":
            case "husky":
            case "shepherd dog":
                return "engine";

            // 포식자: 군중 소음을 싫어함
            case "lion":
            case "lioness":
            case "tiger":
            case "white tiger":
            case "jaguar":
            case "grey wolf":
            case "greywolf":
            case "elite wolf":
            case "wolf":
            case "hayena":
            case "crocodile":
                return "crowd";

            // 극지 동물
            case "polar bear":
            case "pinguin":
            case "penguin":
                return "loud";

            // 기타
            case "boar":
            case "brown bear":
            case "grizzly":
            case "pig":
            case "monkey":
            case "chimp":
            case "turtle":
            case "toirtois":
            case "tortoise":
                return "loud";

            default:
                return "loud";
        }
    }

    private string GetSocialLikeKeyForThisSpecies()
    {
        var name = GetBaseName();

        switch (name)
        {
            // 무리 생활
            case "antilope":
            case "bison":
            case "deer":
            case "giraffe":
            case "oryx":
            case "ram":
            case "sheep":
            case "stag":
            case "water buffalo":
            case "zebra":
            case "cow":
            case "cow brown":
                return "same_species_group";

            // 사람과 함께 일하는 동물
            case "arabian horse":
            case "brown horse":
            case "work horse":
            case "donkey":
            case "camel":
            case "yak":
            case "jak":
            case "lama":
            case "alpacha":
                return "few_humans";

            // 반려견
            case "golden retriever":
            case "husky":
            case "shepherd dog":
                return "with_humans";

            // 고양이
            case "cat black":
            case "cat large":
            case "cat orange":
            case "cat white":
                return "calm_humans";

            // 무리형 극지/영장류
            case "pinguin":
            case "penguin":
            case "monkey":
            case "chimp":
                return "group_social";

            // 포식자: 대체로 단독 선호
            case "lion":
            case "lioness":
            case "tiger":
            case "white tiger":
            case "jaguar":
            case "grey wolf":
            case "greywolf":
            case "elite wolf":
            case "wolf":
            case "hayena":
            case "crocodile":
            case "brown bear":
            case "grizzly":
            case "polar bear":
                return "solitary";

            default:
                return "calm_humans";
        }
    }

    private string GetSocialDislikeKeyFromLikeKey(string likeKey)
    {
        switch (likeKey)
        {
            case "with_humans":
            case "few_humans":
            case "calm_humans":
            case "same_species_group":
            case "group_social":
                return "isolation";

            case "solitary":
                return "crowd";

            default:
                return "crowd";
        }
    }

    private string GetInteractionLikeKey()
    {
        switch (personality)
        {
            case AnimalPersonality.Calm:
            case AnimalPersonality.Gentle:
                return "slow_approach";

            case AnimalPersonality.Shy:
            case AnimalPersonality.Anxious:
                return "keep_distance";

            case AnimalPersonality.Aggressive:
                return "short_contact";

            case AnimalPersonality.Proud:
                return "slow_approach";

            case AnimalPersonality.Playful:
            case AnimalPersonality.Curious:
                return "playful_motion";

            default:
                return "slow_approach";
        }
    }

    private string GetInteractionDislikeKeyFromLikeKey(string likeKey)
    {
        switch (likeKey)
        {
            case "slow_approach":
                return "sudden_grab";
            case "keep_distance":
                return "fast_approach";
            case "short_contact":
                return "prolonged_hold";
            case "playful_motion":
                return "rigid_grab";
            default:
                return "sudden_grab";
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyStage1Preset();
    }

    /// <summary>
    /// Stage1 튜토리얼용 기본 프리셋을 자동 적용.
    /// - Likes: Environment / Food / Social / Interaction
    /// - Dislikes: Sound / Social / Interaction
    /// - difficulty, personality, baseStatus는 기본값에서 크게 벗어나지 않음.
    /// </summary>
    private void ApplyStage1Preset()
    {
        // 튜토리얼은 난이도 1 고정 느낌으로.
        difficulty = 1;

        if (likes == null)
            likes = new List<PreferenceItem>();
        if (dislikes == null)
            dislikes = new List<PreferenceItem>();

        likes.Clear();
        dislikes.Clear();

        // Likes
        var envItem = new PreferenceItem
        {
            category = PreferenceCategory.Environment,
            key = GetEnvironmentKeyForThisSpecies(),
            importance = 1
        };
        likes.Add(envItem);

        var foodItem = new PreferenceItem
        {
            category = PreferenceCategory.Food,
            key = GetFoodKeyForThisSpecies(),
            importance = 1
        };
        likes.Add(foodItem);

        var socialLikeKey = GetSocialLikeKeyForThisSpecies();
        var socialItem = new PreferenceItem
        {
            category = PreferenceCategory.Social,
            key = socialLikeKey,
            importance = 1
        };
        likes.Add(socialItem);

        var interactionLikeKey = GetInteractionLikeKey();
        var interactionItem = new PreferenceItem
        {
            category = PreferenceCategory.Interaction,
            key = interactionLikeKey,
            importance = 1
        };
        likes.Add(interactionItem);

        // Dislikes
        var soundItem = new PreferenceItem
        {
            category = PreferenceCategory.Sound,
            key = GetSoundDislikeKeyForThisSpecies(),
            importance = 1
        };
        dislikes.Add(soundItem);

        var socialDislikeItem = new PreferenceItem
        {
            category = PreferenceCategory.Social,
            key = GetSocialDislikeKeyFromLikeKey(socialLikeKey),
            importance = 1
        };
        dislikes.Add(socialDislikeItem);

        var interactionDislikeItem = new PreferenceItem
        {
            category = PreferenceCategory.Interaction,
            key = GetInteractionDislikeKeyFromLikeKey(interactionLikeKey),
            importance = 1
        };
        dislikes.Add(interactionDislikeItem);
    }
#endif
}
