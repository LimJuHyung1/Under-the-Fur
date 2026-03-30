using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    [Header("스테이지 설정")]
    [SerializeField] private StageConfigSO stageConfig;

    [Header("기본(폴백)")]
    [SerializeField] private int maxTurnsFallback = 15;
    [SerializeField] private int requiredCluesFallback = 4;

    [Header("트라우마 진단 규칙(Stage2)")]
    [SerializeField] private int minTriggerCluesToWin = 2;
    [SerializeField] private int minCauseCluesToWin = 1;

    [Header("신뢰")]
    [SerializeField] private int trustMax = 10;

    [Serializable]
    public struct SessionProgressSnapshot
    {
        public int currentTurn;
        public int maxTurns;
        public int heartOpenCount;
        public int heartsToWinAnimal;
        public int currentDay;
        public int daysPerRun;
        public int runSuccessCount;
        public int animalsToWinRun;
        public AnimalSpeciesSO currentSpecies;
        public int trust;
        public int trustMax;
        public int causeClueCount;
        public int triggerClueCount;
        public string lastGuessKey;
        public bool hasCorrectGuess;
    }

    private AnimalAgent currentAnimal;
    private int currentTurn;
    private int heartOpenCount;
    private int currentDay;
    private int daysPerRun;
    private int runSuccessCount;
    private int animalsToWinRun;
    private AnimalSpeciesSO currentSpecies;

    private readonly HashSet<string> usedLikeKeys = new HashSet<string>();
    private readonly HashSet<string> confirmedCauseKeys = new HashSet<string>();
    private readonly HashSet<string> confirmedTriggerKeys = new HashSet<string>();
    private readonly HashSet<string> confirmedReactionKeys = new HashSet<string>();
    private readonly HashSet<string> confirmedSootheKeys = new HashSet<string>();

    private int trust;
    private string lastGuessKey = "";
    private bool hasCorrectGuess = false;
    private bool sessionActive;

    public event Action<string> OnAnimalLine;
    public event Action<int, int, int> OnSessionProgressChanged;
    public event Action<bool, int> OnSessionEnded;
    public event Action<int> OnHeartOpenGained;
    public event Action<SessionProgressSnapshot> OnProgressChanged;
    public event Action<int, bool> OnDiagnosisResult;

    public int CurrentTurn => currentTurn;
    public int HeartOpenCount => heartOpenCount;
    public int CurrentDay => currentDay;
    public int DaysPerRun => daysPerRun;
    public int RunSuccessCount => runSuccessCount;
    public AnimalSpeciesSO CurrentSpecies => currentSpecies;
    public StageConfigSO StageConfig => stageConfig;

    // 메서드 존재 여부 확인
    public void SetStageConfig(StageConfigSO config)
    {
        stageConfig = config;
        RaiseProgressSnapshot();
    }

    public void StartSession(AnimalAgent animal)
    {
        StopSessionInternal();
        currentAnimal = animal;
        if (currentAnimal == null) return;

        currentTurn = 0;
        heartOpenCount = 0;
        usedLikeKeys.Clear();
        confirmedCauseKeys.Clear();
        confirmedTriggerKeys.Clear();
        confirmedReactionKeys.Clear();
        confirmedSootheKeys.Clear();
        trust = 0;
        lastGuessKey = "";
        hasCorrectGuess = false;
        sessionActive = true;
        currentAnimal.OnReply += HandleAnimalReply;

        RaiseProgressSnapshot();
    }

    private int GetMaxTurns() => (stageConfig != null) ? stageConfig.turnsPerAnimal : maxTurnsFallback;
    private int GetCluesToWin() => (stageConfig != null) ? stageConfig.heartsToWinAnimal : requiredCluesFallback;

    public async Task SubmitPlayerTextAsync(string playerText)
    {
        if (string.IsNullOrWhiteSpace(playerText) || !sessionActive || currentAnimal == null) return;
        if (currentTurn >= GetMaxTurns()) return;
        await currentAnimal.AskAsync(playerText.Trim());
    }

    private void HandleAnimalReply(AnimalReply reply)
    {
        if (!sessionActive) return;
        OnAnimalLine?.Invoke(reply.response);
        currentTurn++;
        trust = Mathf.Clamp(trust + reply.trust_delta, 0, trustMax);

        if (reply.is_like)
        {
            string uniqueKey = reply.topic_key;
            if (!string.IsNullOrEmpty(uniqueKey) && !usedLikeKeys.Contains(uniqueKey))
            {
                usedLikeKeys.Add(uniqueKey);
                heartOpenCount++;
                OnHeartOpenGained?.Invoke(heartOpenCount);
            }
            RecordClue(reply);
        }

        RaiseProgressSnapshot();

        if (!string.IsNullOrWhiteSpace(reply.guess_key))
        {
            lastGuessKey = reply.guess_key.Trim();
            if (currentSpecies != null && !string.IsNullOrWhiteSpace(currentSpecies.traumaAnswerKey))
            {
                if (string.Equals(lastGuessKey, currentSpecies.traumaAnswerKey.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    hasCorrectGuess = true;
                    if (IsWinEvidenceSatisfied()) { EndSession(true); return; }
                }
            }
        }

        if (currentTurn >= GetMaxTurns())
            EndSession(hasCorrectGuess && IsWinEvidenceSatisfied());
        else if (hasCorrectGuess && IsWinEvidenceSatisfied())
            EndSession(true);
    }

    private void RecordClue(AnimalReply reply)
    {
        if (string.IsNullOrWhiteSpace(reply.clue_type) || string.IsNullOrWhiteSpace(reply.clue_key)) return;
        string t = reply.clue_type.Trim().ToLowerInvariant();
        string k = reply.clue_key.Trim();
        switch (t)
        {
            case "cause": confirmedCauseKeys.Add(k); break;
            case "trigger": confirmedTriggerKeys.Add(k); break;
            case "reaction": confirmedReactionKeys.Add(k); break;
            case "soothe": confirmedSootheKeys.Add(k); break;
        }
    }

    private bool IsWinEvidenceSatisfied()
    {
        if (heartOpenCount < GetCluesToWin()) return false;
        return confirmedCauseKeys.Count >= minCauseCluesToWin && confirmedTriggerKeys.Count >= minTriggerCluesToWin;
    }

    private void EndSession(bool success)
    {
        if (!sessionActive) return;
        sessionActive = false;
        if (currentAnimal != null) currentAnimal.OnReply -= HandleAnimalReply;
        OnSessionEnded?.Invoke(success, heartOpenCount);
    }

    private void StopSessionInternal()
    {
        if (currentAnimal != null) currentAnimal.OnReply -= HandleAnimalReply;
        currentAnimal = null;
        sessionActive = false;
        RaiseProgressSnapshot();
    }

    public void SetRunContext(int currentDay, int daysPerRun, int runSuccessCount, int animalsToWinRun)
    {
        this.currentDay = currentDay;
        this.daysPerRun = daysPerRun;
        this.runSuccessCount = runSuccessCount;
        this.animalsToWinRun = animalsToWinRun;
        RaiseProgressSnapshot();
    }

    public void SetCurrentSpecies(AnimalSpeciesSO species)
    {
        currentSpecies = species;
        RaiseProgressSnapshot();
    }

    private void RaiseProgressSnapshot()
    {
        if (OnProgressChanged == null) return;
        OnProgressChanged.Invoke(new SessionProgressSnapshot
        {
            currentTurn = currentTurn,
            maxTurns = GetMaxTurns(),
            heartOpenCount = heartOpenCount,
            heartsToWinAnimal = GetCluesToWin(),
            currentDay = currentDay,
            daysPerRun = daysPerRun,
            runSuccessCount = runSuccessCount,
            animalsToWinRun = animalsToWinRun,
            currentSpecies = currentSpecies,
            trust = trust,
            trustMax = trustMax,
            causeClueCount = confirmedCauseKeys.Count,
            triggerClueCount = confirmedTriggerKeys.Count,
            lastGuessKey = lastGuessKey,
            hasCorrectGuess = hasCorrectGuess
        });
    }

    public async Task SubmitFinalDiagnosisAsync(string playerGuess)
    {
        if (!sessionActive || currentAnimal == null) return;
        AnimalReply result = await currentAnimal.EvaluateFinalDiagnosisAsync(playerGuess);
        OnDiagnosisResult?.Invoke(result.match_percentage, result.match_percentage >= 75);
        EndSession(result.match_percentage >= 75);
    }
}