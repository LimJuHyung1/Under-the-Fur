using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class AnimalAgent : MonoBehaviour
{
    [Header("고정 데이터(SO)")]
    [SerializeField] private AnimalSpeciesSO species;

    public event Action<string> OnPlayerAsked;
    private string cachedSystemPrompt;

    // 기존의 LocalizedString 변수들을 지우고, 일반 string으로 변경해!
    [Header("System Prompts (고정 지시문)")]
    [TextArea(3, 5)]
    public string systemPromptBase =
    "You are a traumatized animal in the game 'Under the Fur'. " +
    "Respond strictly from the animal's perspective based on your personality and trauma. " +
    "**Keep your responses very brief (1-2 sentences).** " + // 길이 제한 추가
    "Focus on emotional expression and subtle hints rather than long explanations. " + // 태도 지정
    "Never reveal you are an AI.";
    [TextArea(1, 2)]
    public string languageInstruction = "Please respond in the following language code: {0}";
    [TextArea(3, 5)]
    public string outputFormat = "You must respond strictly in JSON format. Example: {\"response\": \"...\", \"clues\": [{\"type\": \"...\", \"key\": \"...\", \"evidence\": \"...\"}], \"trust_delta\": \"0\", \"guess_key\": \"\"}";
    
    [Header("컴포넌트(자동 연결 가능)")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animator 트리거 이름")]
    [SerializeField] private string triggerGood = "Good";
    [SerializeField] private string triggerBad = "Bad";
    [SerializeField] private string triggerNeutral = "Neutral";

    [Header("대답 시 공격(말하기) 트리거")]
    [SerializeField] private bool playAttackOnReply = true;
    [SerializeField] private float emotionTriggerDelay = 0.05f;
    private Coroutine emotionRoutine;

    public enum Direction8
    {
        North, South, East, West, NorthEast, NorthWest, SouthEast, SouthWest
    }

    [Header("Directional Animator Control (Bool 기반)")]
    [SerializeField] private bool useDirectionalBoolAnimator = true;
    [SerializeField] private Direction8 defaultFacing = Direction8.North;
    [SerializeField] private float replyAttackDuration = 0.35f;

    private string currentDirectionBool = "isNorth";
    private Coroutine attackRoutine;

    private static readonly string[] DirSuffix =
    {
        "North","South","East","West","NorthEast","NorthWest","SouthEast","SouthWest"
    };

    [Header("Affinity UI (World Space Canvas)")]
    [SerializeField] private Canvas affinityCanvas;
    [SerializeField] private Image affinityFillImage;
    [SerializeField] private float fillLerpSpeed = 8f;
    [SerializeField] private bool billboardToCamera = true;

    private SessionManager sessionManager;
    private bool requestInFlight;
    private float targetFillAmount;

    public IChatService ChatService { get; set; }
    public event Action<AnimalReply> OnReply;

    public AnimalSpeciesSO Species => species;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (affinityFillImage != null)
        {
            affinityFillImage.type = Image.Type.Filled;
            affinityFillImage.fillAmount = 0f;
        }
        if (useDirectionalBoolAnimator) ResetFacingProgressToStart();
        targetFillAmount = 0f;
        UpdateCanvasVisible(false);
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (sessionManager != null)
        {
            sessionManager.OnProgressChanged += HandleProgressChanged;
            sessionManager.OnSessionEnded += HandleSessionEnded;
        }
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        UnbindSession();
    }

    private async void OnLocaleChanged(Locale locale)
    {
        // [핵심 변경] 언어 변경 시 비동기로 프롬프트 재구성
        cachedSystemPrompt = await BuildSystemPromptAsync(species);
        Debug.Log($"[AnimalAgent] Locale changed to: {locale.Identifier.Code}. Prompt rebuilt.");
    }

    private void LateUpdate()
    {
        if (!billboardToCamera || affinityCanvas == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        affinityCanvas.transform.forward = cam.transform.forward;
    }

    private void Update()
    {
        if (affinityFillImage == null) return;
        affinityFillImage.fillAmount = Mathf.Lerp(affinityFillImage.fillAmount, targetFillAmount, Time.deltaTime * fillLerpSpeed);
    }

    public void Init(AnimalSpeciesSO newSpecies)
    {
        species = newSpecies;
        if (animator != null && species != null && species.AnimatorController != null)
            animator.runtimeAnimatorController = species.AnimatorController;

        ResetAffinityUI();
        if (useDirectionalBoolAnimator) ResetFacingProgressToStart();

        // [핵심 변경] 스폰 시점에는 캐시만 초기화합니다.
        cachedSystemPrompt = null;

        if (ChatService is ChatServiceOpenAI svc) svc.ResetContextNow();
    }

    public void BindSession(SessionManager newSession)
    {
        if (sessionManager == newSession) return;
        UnbindSession();
        sessionManager = newSession;
        if (sessionManager == null) return;

        sessionManager.OnProgressChanged += HandleProgressChanged;
        sessionManager.OnSessionEnded += HandleSessionEnded;

        UpdateCanvasVisible(false);
        ResetAffinityUI();
    }

    private void UnbindSession()
    {
        if (sessionManager == null) return;
        sessionManager.OnProgressChanged -= HandleProgressChanged;
        sessionManager.OnSessionEnded -= HandleSessionEnded;
        sessionManager = null;
    }

    private void HandleProgressChanged(SessionManager.SessionProgressSnapshot s)
    {
        if (species == null || s.currentSpecies != species) return;
        UpdateCanvasVisible(true);
        if (s.heartsToWinAnimal <= 0) { targetFillAmount = 0f; return; }
        targetFillAmount = Mathf.Clamp01((float)s.heartOpenCount / s.heartsToWinAnimal);
    }

    private void HandleSessionEnded(bool success, int heartOpenCount)
    {
        UpdateCanvasVisible(false);
        targetFillAmount = 0f;
        if (affinityFillImage != null) affinityFillImage.fillAmount = 0f;
    }

    private void ResetAffinityUI()
    {
        targetFillAmount = 0f;
        if (affinityFillImage != null) affinityFillImage.fillAmount = 0f;
        UpdateCanvasVisible(false);
    }

    private void UpdateCanvasVisible(bool visible)
    {
        if (affinityCanvas != null) affinityCanvas.enabled = visible;
    }

    public async Task AskAsync(string playerText)
    {
        if (requestInFlight || string.IsNullOrWhiteSpace(playerText)) return;
        if (species == null) { EmitImmediateReply("...", ""); return; }
        if (ChatService == null) { EmitImmediateReply("(AI 서비스 연결 안 됨)", ""); return; }

        OnPlayerAsked?.Invoke(playerText);
        requestInFlight = true;

        try
        {
            // [핵심 변경] 질문하는 시점에 프롬프트가 없으면 비동기로 확실히 로드
            if (string.IsNullOrEmpty(cachedSystemPrompt))
            {
                cachedSystemPrompt = await BuildSystemPromptAsync(species);
            }

            string raw = await ChatService.GetResponseAsync(cachedSystemPrompt, playerText);
            var reply = ParseJsonSafe(raw);
            ValidateReplyWithPlayerText(ref reply, playerText);

            PlayReaction(reply);
            OnReply?.Invoke(reply);
        }
        finally { requestInFlight = false; }
    }

    private void EmitImmediateReply(string line, string raw)
    {
        var reply = new AnimalReply
        {
            response = line ?? "",
            topic_key = "",
            evidence = "",
            is_like = false,
            is_dislike = false,
            raw = raw ?? "",
            clue_type = "",
            clue_key = "",
            trust_delta = 0,
            guess_key = ""
        };
        PlayReaction(reply);
        OnReply?.Invoke(reply);
    }

    private void PlayReaction(AnimalReply reply)
    {
        if (animator == null) return;

        if (useDirectionalBoolAnimator)
        {
            SetRandomFacing();
            if (playAttackOnReply) PlayAttackOnce();
        }

        if (emotionRoutine != null) StopCoroutine(emotionRoutine);
        emotionRoutine = StartCoroutine(CoPlayEmotionTriggerAfterDelay(reply, emotionTriggerDelay));
    }

    private void SetRandomFacing()
    {
        if (!useDirectionalBoolAnimator || animator == null) return;
        int randomDirIndex = UnityEngine.Random.Range(0, 8);
        Direction8 randomDir = (Direction8)randomDirIndex;
        SetFacing(randomDir, false);
    }

    public void SetFacing(Direction8 dir, bool returnIdle = true)
    {
        if (!useDirectionalBoolAnimator || animator == null) return;
        currentDirectionBool = "is" + dir.ToString();
        RestoreFacing();
        if (returnIdle) PlayIdle();
    }

    private void RestoreFacing()
    {
        if (animator == null) return;
        foreach (var suffix in DirSuffix)
        {
            TrySetBool("is" + suffix, false);
        }
        TrySetBool(currentDirectionBool, true);
    }

    public void PlayIdle()
    {
        if (!useDirectionalBoolAnimator || animator == null) return;
        ClearAllAttackParameters();
        RestoreFacing();
    }

    public void PlayAttackOnce()
    {
        if (!useDirectionalBoolAnimator || animator == null || !gameObject.activeInHierarchy) return;
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        ClearAllAttackParameters();

        string suffix = currentDirectionBool.Substring(2);
        string attackParam = (UnityEngine.Random.Range(0, 2) == 0 ? "AttackAttack" : "Attack2") + suffix;

        TrySetBool(attackParam, true);
        TrySetBool("isAttackAttacking", true);
        attackRoutine = StartCoroutine(CoResetAttackAfter(replyAttackDuration, attackParam));
    }

    private void ClearAllAttackParameters()
    {
        TrySetBool("isAttackAttacking", false);
        foreach (var suffix in DirSuffix)
        {
            TrySetBool("AttackAttack" + suffix, false);
            TrySetBool("Attack2" + suffix, false);
        }
    }

    private IEnumerator CoResetAttackAfter(float t, string activatedParam)
    {
        yield return new WaitForSeconds(t);
        TrySetBool("isAttackAttacking", false);
        TrySetBool(activatedParam, false);
        RestoreFacing();
        attackRoutine = null;
    }

    private IEnumerator CoPlayEmotionTriggerAfterDelay(AnimalReply reply, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayEmotionTriggerNow(reply);
        emotionRoutine = null;
    }

    private void PlayEmotionTriggerNow(AnimalReply reply)
    {
        if (species != null && audioSource != null)
        {
            AudioClip clip = species.GetRandomCry();
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        if (animator == null) return;

        if (reply.is_dislike)
        {
            if (!string.IsNullOrEmpty(triggerBad)) animator.SetTrigger(triggerBad);
            return;
        }
        if (reply.is_like)
        {
            if (!string.IsNullOrEmpty(triggerGood)) animator.SetTrigger(triggerGood);
            return;
        }
        if (!string.IsNullOrEmpty(triggerNeutral)) animator.SetTrigger(triggerNeutral);
    }

    private async Task<string> BuildSystemPromptAsync(AnimalSpeciesSO s)
    {
        if (s == null) return "";

        string currentLanguage = LocalizationSettings.SelectedLocale != null
            ? LocalizationSettings.SelectedLocale.Identifier.Code
            : "en";

        // [수정됨] 에러가 나던 다국어 호출 코드를 지우고, 맨 위에서 만든 일반 string 변수를 바로 연결!
        string basePrompt = systemPromptBase;
        string formatInstruction = outputFormat;
        string langInstruction = string.Format(languageInstruction, currentLanguage);

        // [유지됨] 동물의 이름과 트라우마는 비동기로 다국어 로딩을 잘 기다리고 있어!
        string animalName = await s.localizedDisplayName.GetLocalizedStringAsync().Task;
        string animalTrauma = await s.loc_traumaSummaryForAI.GetLocalizedStringAsync().Task;

        string causeStr = s.traumaCauseKeys != null ? string.Join(", ", s.traumaCauseKeys) : "";
        string triggerStr = s.traumaTriggerKeys != null ? string.Join(", ", s.traumaTriggerKeys) : "";
        string reactionStr = s.traumaReactionKeys != null ? string.Join(", ", s.traumaReactionKeys) : "";
        string sootheStr = s.traumaSootheKeys != null ? string.Join(", ", s.traumaSootheKeys) : "";

        return $"{basePrompt}\n\n" +
               $"[Language Setting]\n{langInstruction}\n\n" +
               $"{formatInstruction}\n\n" +
               $"[Animal Profile]\n" +
               $"- Name: {animalName}\n" +
               $"- Trauma Context: {animalTrauma}\n" +
               $"- Personality: {s.personality}\n" +
               $"- Current Status: {s.baseStatus}\n\n" +
               $"[Allowed Clue Keys (Must use these exact keys if providing a clue)]\n" +
               $"- cause: {causeStr}\n" +
               $"- trigger: {triggerStr}\n" +
               $"- reaction: {reactionStr}\n" +
               $"- soothe: {sootheStr}";
    }
    private void ValidateReplyWithPlayerText(ref AnimalReply reply, string playerText)
    {
        reply.response = SafeTrim(reply.response);
        if (string.IsNullOrEmpty(reply.response)) reply.response = "...";
        reply.trust_delta = Mathf.Clamp(reply.trust_delta, -2, 2);

        bool hasClue = !string.IsNullOrWhiteSpace(reply.clue_type) && !string.IsNullOrWhiteSpace(reply.clue_key);
        if (hasClue)
        {
            string type = SafeTrim(reply.clue_type).ToLowerInvariant();
            string key = SafeTrim(reply.clue_key);
            string ev = SafeTrim(reply.evidence);

            if (!IsAllowedClueKey(species, type, key) || ev.Length < 2 || !reply.response.Contains(ev))
            {
                reply.clue_type = ""; reply.clue_key = ""; reply.evidence = "";
                reply.topic_key = ""; reply.is_like = false;
            }
            else
            {
                reply.topic_key = type + ":" + key;
                reply.is_like = true;
            }
        }
        reply.guess_key = IsGuessAttempt(playerText) && IsAllowedGuessKey(species, reply.guess_key) ? SafeTrim(reply.guess_key) : "";
    }

    private AnimalReply ParseJsonSafe(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new AnimalReply { response = "..." };
        string json = ExtractFirstJsonObject(raw) ?? raw;

        try
        {
            AnimalReplyDTO dto = JsonUtility.FromJson<AnimalReplyDTO>(json);
            string clueType = "", clueKey = "", evidence = "";

            if (dto.clues != null && dto.clues.Length > 0 && dto.clues[0] != null)
            {
                clueType = dto.clues[0].type ?? "";
                clueKey = dto.clues[0].key ?? "";
                evidence = dto.clues[0].evidence ?? "";
            }

            return new AnimalReply
            {
                response = dto.response ?? "",
                evidence = evidence,
                raw = raw,
                clue_type = clueType,
                clue_key = clueKey,
                trust_delta = ParseIntSafe(dto.trust_delta, 0),
                guess_key = dto.guess_key ?? "",
                match_percentage = dto.match_percentage,
                eval_reason = dto.eval_reason ?? ""
            };
        }
        catch { return new AnimalReply { response = raw.Trim(), raw = raw }; }
    }

    private static string ExtractFirstJsonObject(string text)
    {
        int start = text.IndexOf('{');
        if (start < 0) return null;
        int depth = 0;
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') { depth--; if (depth == 0) return text.Substring(start, i - start + 1); }
        }
        return null;
    }

    [Serializable]
    private class AnimalReplyDTO
    {
        public string response;
        public ClueDTO[] clues;
        public string trust_delta;
        public string guess_key;
        public int match_percentage;
        public string eval_reason;
    }

    [Serializable] private class ClueDTO { public string type; public string key; public string evidence; }

    private static int ParseIntSafe(string s, int fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        s = s.Replace("\"", "").Trim();
        return int.TryParse(s, out int v) ? v : fallback;
    }

    private static string SafeTrim(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

    private void ResetFacingProgressToStart()
    {
        SetFacing(defaultFacing, true);
    }

    private void TrySetBool(string name, bool value) { if (animator != null) animator.SetBool(name, value); }

    private static bool IsGuessAttempt(string t) => !string.IsNullOrWhiteSpace(t) && (t.Contains("진단") || t.Contains("결론") || t.Contains("트라우마") || t.Contains("Diagnosis") || t.Contains("診断"));

    private static bool IsAllowedGuessKey(AnimalSpeciesSO s, string guessKey)
    {
        if (s == null || string.IsNullOrEmpty(guessKey)) return false;
        if (s.traumaCandidateKeys != null)
            foreach (var k in s.traumaCandidateKeys) if (string.Equals(k.Trim(), guessKey.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(s.traumaAnswerKey?.Trim(), guessKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedClueKey(AnimalSpeciesSO s, string type, string key)
    {
        if (s == null) return false;
        List<string> list = type switch { "cause" => s.traumaCauseKeys, "trigger" => s.traumaTriggerKeys, "reaction" => s.traumaReactionKeys, "soothe" => s.traumaSootheKeys, _ => null };
        if (list == null) return false;
        foreach (var allowed in list) if (string.Equals(allowed.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public async Task<AnimalReply> EvaluateFinalDiagnosisAsync(string playerGuess)
    {
        if (species == null || ChatService == null) return default;
        // 최종 진단 평가 시에도 로컬라이즈된 동물의 트라우마 정보를 사용합니다.
        string evalPrompt = $"You are an expert evaluator. [Correct Answer]{species.traumaSummaryForAI} [Player Guess]{playerGuess} Score 0-100. JSON only: {{\\\"match_percentage\\\": score, \\\"eval_reason\\\": \\\"reason\\\", \\\"response\\\": \\\"response\\\"}}";
        string raw = await ChatService.GetOneShotAsync(evalPrompt, "Evaluate now.");
        return ParseJsonSafe(raw);
    }
}

[System.Serializable]
public struct AnimalReply
{
    public string response;
    public string topic_key;
    public string evidence;
    public bool is_like;
    public bool is_dislike;
    public int match_percentage;
    public string eval_reason;
    public string clue_type;
    public string clue_key;
    public int trust_delta;
    public string guess_key;
    public string raw;
}
