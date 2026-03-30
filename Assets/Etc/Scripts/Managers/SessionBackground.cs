using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SessionBackground : MonoBehaviour
{
    [Serializable]
    public class EnvironmentSpriteSet
    {
        public string environmentKey;        // 예: "desert"
        public List<Sprite> sprites = new List<Sprite>(); // 시간대 7장 (아침~밤)
    }

    [Header("Refs")]
    [SerializeField] private SessionManager session;

    [Header("Target (둘 중 하나만 연결해도 됨)")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [SerializeField] private Image targetUIImage;

    [Header("Environment Sprite Sets (각 환경별 7장)")]
    [SerializeField] private List<EnvironmentSpriteSet> environmentSets = new List<EnvironmentSpriteSet>();

    [Header("Turn Rule")]
    [SerializeField] private int turnsPerBackgroundStep = 2; // 플레이어 2턴마다 배경 변경
    [SerializeField] private string defaultEnvironmentKey = "forest";

    private AnimalSpeciesSO currentSpecies;
    private string currentEnvKey;
    private int lastAppliedIndex = -1;

    [SerializeField] private BackgroundFitToCamera fitter;

    private void Awake()
    {
        if (session == null)
            session = FindFirstObjectByType<SessionManager>();

        if (targetSpriteRenderer == null)
            targetSpriteRenderer = GetComponent<SpriteRenderer>();

        if (fitter == null)
            fitter = GetComponent<BackgroundFitToCamera>();
    }

    private void OnEnable()
    {
        if (session == null)
            session = FindFirstObjectByType<SessionManager>();

        if (session != null)
            session.OnProgressChanged += HandleProgressChanged;
    }

    private void OnDisable()
    {
        if (session != null)
            session.OnProgressChanged -= HandleProgressChanged;
    }

    private void HandleProgressChanged(SessionManager.SessionProgressSnapshot s)
    {
        // 세션에서 현재 동물 정보가 넘어오지 않으면 종료
        if (s.currentSpecies == null)
            return;

        // 동물이 바뀌면 환경/인덱스 리셋
        if (currentSpecies != s.currentSpecies)
        {
            currentSpecies = s.currentSpecies;
            currentEnvKey = GetEnvKeySafe(currentSpecies);
            lastAppliedIndex = -1;

            // 세션 시작 시점에 바로 0번 이미지(아침)를 보여주고 싶으면 여기서 한 번 적용
            ApplyBackgroundByTurn(s.currentTurn);
            return;
        }

        ApplyBackgroundByTurn(s.currentTurn);
    }

    private void ApplyBackgroundByTurn(int currentTurn)
    {
        // currentTurn 정의가 프로젝트마다 다를 수 있음:
        // 보통 0에서 시작해서 플레이어가 입력하면 1,2,... 증가.
        // currentTurn이 0이면 "첫 배경"으로 세팅
        int index = TurnToIndex(currentTurn);

        if (index == lastAppliedIndex)
            return;

        Sprite sprite = GetSpriteFromEnv(currentEnvKey, index);
        if (sprite == null)
            return;

        SetBackgroundSprite(sprite);
        lastAppliedIndex = index;
    }

    private int TurnToIndex(int currentTurn)
    {
        // 2턴마다 1단계씩 증가
        // turn=0 -> index 0
        // turn=1,2 -> index 0
        // turn=3,4 -> index 1
        // ...
        // 프로젝트 턴 기준이 다르면 여기만 조정하면 됨.

        if (turnsPerBackgroundStep <= 0)
            turnsPerBackgroundStep = 2;

        int t = Mathf.Max(0, currentTurn);

        // 0일 때도 0번 이미지로 보이게
        if (t == 0) return 0;

        int idx = (t - 1) / turnsPerBackgroundStep;

        // 7장 기준(0~6). 15턴이면 idx가 7까지 갈 수 있어서 clamp로 마지막 유지.
        idx = Mathf.Clamp(idx, 0, 6);
        return idx;
    }

    private string GetEnvKeySafe(AnimalSpeciesSO species)
    {
        if (species == null)
            return defaultEnvironmentKey;

        // AnimalSpeciesSO에 아래 중 하나를 추가해둔 상태를 가정
        // - public string EnvironmentKey => ...
        // - public string GetEnvironmentKey() => ...
        string key = null;

        // 프로퍼티 방식
        try
        {
            key = species.EnvironmentKey;
        }
        catch { }

        if (string.IsNullOrEmpty(key))
            key = defaultEnvironmentKey;

        return key.Trim().ToLowerInvariant();
    }

    private Sprite GetSpriteFromEnv(string envKey, int index)
    {
        if (string.IsNullOrEmpty(envKey))
            envKey = defaultEnvironmentKey;

        EnvironmentSpriteSet set = null;

        for (int i = 0; i < environmentSets.Count; i++)
        {
            if (environmentSets[i] == null) continue;

            string k = environmentSets[i].environmentKey;
            if (string.IsNullOrEmpty(k)) continue;

            if (string.Equals(k.Trim(), envKey, StringComparison.OrdinalIgnoreCase))
            {
                set = environmentSets[i];
                break;
            }
        }

        // 환경 키가 없으면 default로 fallback
        if (set == null && !string.Equals(envKey, defaultEnvironmentKey, StringComparison.OrdinalIgnoreCase))
            return GetSpriteFromEnv(defaultEnvironmentKey, index);

        if (set == null) return null;
        if (set.sprites == null) return null;
        if (set.sprites.Count == 0) return null;

        int safeIndex = Mathf.Clamp(index, 0, set.sprites.Count - 1);
        return set.sprites[safeIndex];
    }

    private void SetBackgroundSprite(Sprite sprite)
    {
        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.sprite = sprite;

            // 스프라이트 바뀐 뒤, 화면에 딱 맞게 다시 스케일 조정
            if (fitter != null)
                fitter.FitNow();

            return;
        }

        if (targetUIImage != null)
        {
            targetUIImage.sprite = sprite;
            return;
        }
    }

}
