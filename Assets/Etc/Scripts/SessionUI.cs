using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SessionUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SessionManager sessionManager;

    [Header("Input")]
    [SerializeField] private InputField inputField;
    [SerializeField] private bool submitOnlyOnEnter = true;
    [SerializeField] private bool lockInputWhileRequest = true;

    [Header("Texts")]
    [SerializeField] private Text animalAnswerText;

    [Header("Stage2 HUD (Optional)")]
    [SerializeField] private Text turnText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text hintText;
    [SerializeField] private bool showDiagnosisHint = true;

    [Header("Run Day UI (RunDay/Day1~Day7)")]
    [SerializeField] private Transform runDayRoot;

    [Header("Hidden Portrait (Placeholder)")]
    [SerializeField] private Sprite hiddenPortraitSprite;

    [Header("Default / Reveal Alpha")]
    [SerializeField] private float defaultAlpha = 0.5f;
    [SerializeField] private float revealAlpha = 1.0f;

    [Header("Final Diagnosis UI")]
    [SerializeField] private Button diagnosisButton;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultScoreText;

    [Header("Final Diagnosis Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.red;

    [Header("Run Count UI")]
    [SerializeField] private int defaultAnimalsToWinRun = 4;
    [SerializeField] private Color countNormalColor = new Color32(255, 69, 0, 255);
    [SerializeField] private Color countCompletedColor = new Color32(0, 250, 154, 255);

    private Text runCountText;

    [Header("Localized Placeholders")]
    [SerializeField] private LocalizedString loc_diagnosisPlaceholder = new LocalizedString { TableReference = "StringTable", TableEntryReference = "Stage2-SeesionUI-DiagnosisPlaceholde" };
    [SerializeField] private LocalizedString loc_defaultPlaceholder = new LocalizedString { TableReference = "StringTable", TableEntryReference = "Stage2-SeesionUI-DefaultPlaceholde" };
    [SerializeField] private LocalizedString loc_diagnosisLabel = new LocalizedString { TableReference = "StringTable", TableEntryReference = "Stage2-SessionUI-DiagnosisLabel" };
    [SerializeField] private LocalizedString loc_similarityLabel = new LocalizedString { TableReference = "StringTable", TableEntryReference = "Stage2-SessionUI-SimilarityLabel" };
    [SerializeField] private LocalizedString loc_successMsg = new LocalizedString { TableReference = "StringTable", TableEntryReference = "Stage2-SessionUI-SuccessMsg" };
    [SerializeField] private LocalizedString loc_failMsg = new LocalizedString { TableReference = "StringTable", TableEntryReference = "Stage2-SessionUI-FailMsg" };

    [Header("Hardcoded Strings (Legacy)")]
    [SerializeField] private string turnPrefix = "턴";
    [SerializeField] private string statusClue = "단서";
    [SerializeField] private string statusCause = "원인";
    [SerializeField] private string statusTrigger = "트리거";
    [SerializeField] private string statusTrust = "신뢰";
    [SerializeField] private string statusCorrect = "정답";
    [SerializeField] private string statusSubmitted = "제출됨";
    [TextArea]
    [SerializeField] private string hintMsg = "동물의 행동 원인과 트리거를 파악한 뒤 '진단' 버튼을 눌러 결론을 입력하세요.";

    [Header("Run Management")]
    [SerializeField] private Button stopAndSaveButton; // 중단하기 버튼
    [SerializeField] private Button restartRunButton;  // 다시하기 버튼

    private bool isDiagnosisMode = false;
    private Text placeholderText;
    private bool requestInFlight;
    private bool suppressNextEndEditSubmit;

    private DaySlot[] daySlots = new DaySlot[7];
    private int lastDay = -1;
    private int lastAnimatedDay = -1;

    private class DaySlot
    {
        public GameObject root;
        public CanvasGroup canvasGroup;
        public Image portrait;
        public DOTweenAnimation portraitAnim;
        public string appliedSpeciesId;
        public Coroutine hideSlotRoutine;

        public GameObject oMark;
        public GameObject xMark;
    }

    private void Awake()
    {
        if (sessionManager == null)
            sessionManager = FindFirstObjectByType<SessionManager>();

        if (diagnosisButton != null)
            diagnosisButton.onClick.AddListener(OnClickDiagnoseButton);

        if (sessionManager != null)
            sessionManager.OnDiagnosisResult += ShowDiagnosisResult;

        if (inputField != null)
            placeholderText = inputField.placeholder.GetComponent<Text>();

        if (stopAndSaveButton != null)
            stopAndSaveButton.onClick.AddListener(OnClickStopAndSave);

        if (restartRunButton != null)
            restartRunButton.onClick.AddListener(OnClickRestartRun);

        CacheDaySlots();
        ResetAllDaySlotsImmediate();
    }

    private void OnEnable()
    {
        if (inputField != null)
            inputField.onEndEdit.AddListener(OnEndEdit);

        if (sessionManager != null)
        {
            sessionManager.OnAnimalLine += HandleAnimalLine;
            sessionManager.OnProgressChanged += HandleProgressChanged;
            sessionManager.OnSessionEnded += HandleSessionEnded;
        }

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        UpdatePlaceholder();
        UpdateHintText();
    }

    private void OnDisable()
    {
        if (inputField != null)
            inputField.onEndEdit.RemoveListener(OnEndEdit);

        if (sessionManager != null)
        {
            sessionManager.OnAnimalLine -= HandleAnimalLine;
            sessionManager.OnProgressChanged -= HandleProgressChanged;
            sessionManager.OnSessionEnded -= HandleSessionEnded;
        }

        StopAllSlotCoroutines();
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void HandleSessionEnded(bool success, int heartOpenCount)
    {
        if (sessionManager == null) return;

        int day = sessionManager.CurrentDay;
        if (day < 1 || day > 7) return;

        SetDayResultMark(daySlots[day - 1], success);
    }

    private void ApplySavedDayResult(DaySlot slot, int result)
    {
        switch (result)
        {
            case 1:
                SetDayResultMark(slot, true);
                break;

            case 2:
                SetDayResultMark(slot, false);
                break;

            default:
                SetDayResultMark(slot, null);
                break;
        }
    }

    // [추가] 언어가 변경될 때 즉시 텍스트를 갱신
    private void OnLocaleChanged(Locale locale)
    {
        UpdatePlaceholder();
    }

    private void Start()
    {
        FocusInput();
    }

    private void CacheDaySlots()
    {
        for (int i = 0; i < 7; i++)
            daySlots[i] = new DaySlot();

        if (runDayRoot == null) return;

        for (int day = 1; day <= 7; day++)
        {
            Transform dayTf = runDayRoot.Find("Day" + day);
            if (dayTf == null) continue;

            DaySlot slot = daySlots[day - 1];
            slot.root = dayTf.gameObject;
            slot.canvasGroup = dayTf.GetComponent<CanvasGroup>() ?? dayTf.gameObject.AddComponent<CanvasGroup>();

            Transform portraitTf = dayTf.Find("Portrait");
            if (portraitTf != null)
            {
                slot.portrait = portraitTf.GetComponent<Image>();
                slot.portraitAnim = portraitTf.GetComponent<DOTweenAnimation>();
            }

            Transform oTf = dayTf.Find("O");
            if (oTf != null)
                slot.oMark = oTf.gameObject;

            Transform xTf = dayTf.Find("X");
            if (xTf != null)
                slot.xMark = xTf.gameObject;
        }

        Transform countTf = runDayRoot.Find("Count");
        if (countTf != null)
        {
            runCountText = countTf.GetComponent<Text>();
            if (runCountText == null)
                runCountText = countTf.GetComponentInChildren<Text>(true);
        }
    }

    private void UpdateRunCountText(int successCount, int targetCount)
    {
        if (runCountText == null) return;

        int safeTarget = targetCount > 0 ? targetCount : defaultAnimalsToWinRun;
        int safeSuccess = Mathf.Max(0, successCount);

        runCountText.text = $"{safeSuccess} / {safeTarget}";
        runCountText.color = (safeSuccess >= safeTarget) ? countCompletedColor : countNormalColor;
    }

    private void SetDayResultMark(DaySlot slot, bool? isSuccess)
    {
        if (slot == null) return;

        if (slot.oMark != null)
            slot.oMark.SetActive(isSuccess == true);

        if (slot.xMark != null)
            slot.xMark.SetActive(isSuccess == false);
    }

    private void HandleProgressChanged(SessionManager.SessionProgressSnapshot s)
    {
        if (lastDay != s.currentDay)
        {
            if (animalAnswerText != null)
                animalAnswerText.text = "";
        }

        if (lastDay != -1 && s.currentDay < lastDay)
        {
            ResetAllDaySlotsImmediate();
            lastAnimatedDay = -1;
        }

        if ((lastDay == -1 || lastDay == 0) && s.currentDay > 1)
        {
            RestorePreviousDaysUI(s.currentDay);
        }

        lastDay = s.currentDay;
        UpdateRevealByDayChange(s.currentDay, s.currentSpecies);

        UpdateRunCountText(s.runSuccessCount, s.animalsToWinRun);
        UpdateHudTexts(s);
    }

    // [추가된 메서드] 이전 동물 데이터를 가져와서 초상화를 밝게 켜줍니다.
    private void RestorePreviousDaysUI(int currentDay)
    {
        var runQueue = UnityEngine.Object.FindFirstObjectByType<AnimalRunQueue>();
        if (runQueue == null) return;

        for (int i = 0; i < currentDay - 1; i++)
        {
            var pastSpecies = runQueue.GetRunSpeciesAt(i);
            if (pastSpecies != null && i < 7)
            {
                ApplyRevealStateImmediate(daySlots[i], pastSpecies);
            }

            int result = runQueue.GetRunDayResultAt(i);
            ApplySavedDayResult(daySlots[i], result);
        }
    }

    // [수정] async 키워드를 추가하고 진단 라벨을 다국어 테이블에서 불러옵니다.
    private async void UpdateHudTexts(SessionManager.SessionProgressSnapshot s)
    {
        if (turnText != null)
        {
            turnText.text = $"{turnPrefix} {s.currentTurn} / {Mathf.Max(1, s.maxTurns)}";
        }

        if (statusText != null)
        {
            string cluePart = $"{statusClue} {s.heartOpenCount}/{s.heartsToWinAnimal} | {statusCause} {s.causeClueCount} | {statusTrigger} {s.triggerClueCount}";
            string statePart = $"{statusTrust} {s.trust}/{s.trustMax}";

            string guessStatus = "";
            if (!string.IsNullOrWhiteSpace(s.lastGuessKey))
            {
                string resultText = s.hasCorrectGuess ? statusCorrect : statusSubmitted;

                // 비동기로 다국어 진단 라벨 가져오기
                string diagnosisLabelStr = await loc_diagnosisLabel.GetLocalizedStringAsync().Task;
                guessStatus = $" | {diagnosisLabelStr}: {resultText}";
            }

            statusText.text = $"{cluePart} | {statePart}{guessStatus}";
        }
    }

    private void UpdateRevealByDayChange(int currentDay, AnimalSpeciesSO species)
    {
        if (currentDay < 1 || currentDay > 7) return;

        if (currentDay != lastAnimatedDay)
        {
            lastAnimatedDay = currentDay;
            BeginRevealDay(currentDay, species);
        }
        else
        {
            DaySlot slot = daySlots[currentDay - 1];
            if (slot != null) ApplyRevealStateImmediate(slot, species);
        }
    }

    private void BeginRevealDay(int day, AnimalSpeciesSO species)
    {
        DaySlot slot = daySlots[day - 1];
        if (slot == null) return;
        StopSlotCoroutines(slot);
        ApplyRevealStateImmediate(slot, species);
        if (slot.portraitAnim != null)
            slot.portraitAnim.DORestart();
    }

    private void ApplyHiddenStateImmediate(DaySlot slot)
    {
        if (slot == null) return;
        slot.appliedSpeciesId = "";

        if (slot.root != null) slot.root.SetActive(true);
        if (slot.canvasGroup != null) slot.canvasGroup.alpha = defaultAlpha;

        if (slot.portrait != null)
        {
            slot.portrait.sprite = hiddenPortraitSprite;
            slot.portrait.color = Color.black;
            slot.portrait.enabled = (hiddenPortraitSprite != null);
        }

        SetDayResultMark(slot, null);
    }

    private void ApplyRevealStateImmediate(DaySlot slot, AnimalSpeciesSO species)
    {
        if (slot == null || species == null) return;
        slot.appliedSpeciesId = species.id ?? "";
        if (slot.canvasGroup != null) slot.canvasGroup.alpha = revealAlpha;
        if (slot.portrait != null)
        {
            slot.portrait.sprite = species.portrait ?? hiddenPortraitSprite;
            slot.portrait.color = Color.white;
            slot.portrait.enabled = (slot.portrait.sprite != null);
        }
    }

    private void ResetAllDaySlotsImmediate()
    {
        lastDay = -1;
        lastAnimatedDay = -1;
        for (int i = 0; i < 7; i++)
        {
            StopSlotCoroutines(daySlots[i]);
            ApplyHiddenStateImmediate(daySlots[i]);
        }

        UpdateRunCountText(0, defaultAnimalsToWinRun);
    }

    private void OnEndEdit(string rawText)
    {
        if (suppressNextEndEditSubmit) { suppressNextEndEditSubmit = false; FocusInput(); return; }
        if (submitOnlyOnEnter)
        {
#if UNITY_STANDALONE || UNITY_EDITOR || UNITY_WEBGL
            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
#endif
        }
        _ = SubmitAsync(rawText);
    }

    private async Task SubmitAsync(string rawText)
    {
        if (requestInFlight || sessionManager == null) return;
        string text = rawText?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        requestInFlight = true;
        if (lockInputWhileRequest && inputField != null) inputField.interactable = false;
        if (inputField != null) inputField.text = "";

        try
        {
            if (isDiagnosisMode) { await sessionManager.SubmitFinalDiagnosisAsync(text); ResetDiagnosisMode(); }
            else { await sessionManager.SubmitPlayerTextAsync(text); }
        }
        finally
        {
            if (lockInputWhileRequest && inputField != null) inputField.interactable = true;
            FocusInput();
            requestInFlight = false;
        }
    }

    private void ResetDiagnosisMode()
    {
        isDiagnosisMode = false;
        if (diagnosisButton != null) diagnosisButton.image.color = normalColor;
        UpdatePlaceholder();
    }

    private void OnClickDiagnoseButton()
    {
        isDiagnosisMode = !isDiagnosisMode;
        if (diagnosisButton != null) diagnosisButton.image.color = isDiagnosisMode ? activeColor : normalColor;
        UpdatePlaceholder();
        FocusInput();
    }

    // [수정] async 키워드 추가 및 LocalizedString 비동기 호출 적용
    private async void UpdatePlaceholder()
    {
        if (placeholderText == null) return;

        // 현재 진단 모드 여부에 따라 알맞은 LocalizedString 선택
        LocalizedString activeLocString = isDiagnosisMode ? loc_diagnosisPlaceholder : loc_defaultPlaceholder;

        // 로컬라이제이션 테이블에서 해당 언어의 텍스트를 비동기로 가져와서 적용
        placeholderText.text = await activeLocString.GetLocalizedStringAsync().Task;
    }

    // [수정] async 키워드를 추가하고 일치도 및 성공/실패 메시지를 다국어 테이블에서 불러옵니다.
    private async void ShowDiagnosisResult(int score, bool isSuccess)
    {
        if (resultPanel != null) resultPanel.SetActive(true);
        if (resultScoreText != null)
        {
            // 비동기로 다국어 텍스트 가져오기
            string simLabelStr = await loc_similarityLabel.GetLocalizedStringAsync().Task;
            string resultMsgStr = isSuccess
                ? await loc_successMsg.GetLocalizedStringAsync().Task
                : await loc_failMsg.GetLocalizedStringAsync().Task;

            resultScoreText.text = $"{simLabelStr}: {score}% \n{resultMsgStr}";
        }
        diagnosisButton.interactable = true;
    }

    public void CloseResultPanel() { if (resultPanel != null) resultPanel.SetActive(false); }
    private void FocusInput() { if (inputField != null) inputField.ActivateInputField(); }
    private void HandleAnimalLine(string line) { if (animalAnswerText != null) animalAnswerText.text = line ?? ""; }
    private void StopAllSlotCoroutines() { for (int i = 0; i < 7; i++) StopSlotCoroutines(daySlots[i]); }
    private void StopSlotCoroutines(DaySlot slot)
    {
        if (slot?.hideSlotRoutine != null) { StopCoroutine(slot.hideSlotRoutine); slot.hideSlotRoutine = null; }
    }

    private void UpdateHintText()
    {
        if (hintText == null) return;
        hintText.text = showDiagnosisHint ? hintMsg : "";
    }





    // 1. 중단하기: 현재 상태를 저장하고 메인 화면으로 나갑니다.
    private void OnClickStopAndSave()
    {
        // [핵심] 현재 씬 전체에서 확실하게 RunQueue를 찾습니다.
        var runQueue = UnityEngine.Object.FindFirstObjectByType<AnimalRunQueue>();

        if (runQueue != null)
        {
            runQueue.SaveRunProgress(); // 진행도 저장
            Debug.Log("[SessionUI] 정상적으로 저장이 완료되었습니다!");
        }
        else
        {
            Debug.LogError("[SessionUI] AnimalRunQueue를 찾지 못해 저장에 실패했습니다!");
        }

        // 메인 씬으로 이동
        if (StageSelection.Instance != null)
            StageSelection.LoadSceneByName("Main");
    }

    private void OnClickRestartRun()
    {
        var runQueue = FindFirstObjectByType<AnimalRunQueue>();
        if (runQueue != null)
        {
            runQueue.ClearRunProgress(); // 저장 데이터 삭제
        }

        StageSelection.ReloadCurrentScene();
    }

}
