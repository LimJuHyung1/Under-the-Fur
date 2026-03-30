using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public enum ActionMethod
{
    SAFETY,
    OBSERVE,
    PROBE,
    OFFER_CLOSE
}

public class ActionSentenceGenerator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ChatServiceOpenAI chatService;
    [SerializeField] private InputField inputField;

    [Header("Current Animal (자동/수동 바인딩)")]
    [SerializeField] private AnimalAgent currentAnimal;

    [Header("Conversation Context")]
    [SerializeField] private int maxStoredLines = 40;
    [SerializeField] private int recentContextLines = 8;

    [Header("Sliders (0~2, Whole Numbers 권장)")]
    [SerializeField] private Slider warmthSlider;
    [SerializeField] private Slider riskToleranceSlider;
    [SerializeField] private Slider formalitySlider;
    [SerializeField] private Slider verbositySlider;

    [Header("Optional: show current values")]
    [SerializeField] private Text warmthValueText;
    [SerializeField] private Text riskValueText;
    [SerializeField] private Text formalityValueText;
    [SerializeField] private Text verbosityValueText;

    private bool isGenerating;
    private ActionMethod lastMethod = ActionMethod.SAFETY;

    private struct Line
    {
        public string speaker;
        public string text;
    }

    private readonly List<Line> lines = new List<Line>();

    private void Awake()
    {
        SetupSlider(warmthSlider);
        SetupSlider(riskToleranceSlider);
        SetupSlider(formalitySlider);
        SetupSlider(verbositySlider);

        UpdateValueTexts();

        // currentAnimal이 비어 있으면 씬에서 하나 찾아서 붙인다(한 마리만 있는 구조일 때 편함)
        if (currentAnimal == null)
            currentAnimal = FindFirstObjectByType<AnimalAgent>();
    }

    private void OnEnable()
    {
        if (warmthSlider != null) warmthSlider.onValueChanged.AddListener(OnSliderChanged);
        if (riskToleranceSlider != null) riskToleranceSlider.onValueChanged.AddListener(OnSliderChanged);
        if (formalitySlider != null) formalitySlider.onValueChanged.AddListener(OnSliderChanged);
        if (verbositySlider != null) verbositySlider.onValueChanged.AddListener(OnSliderChanged);

        BindAnimal(currentAnimal);
    }

    private void OnDisable()
    {
        if (warmthSlider != null) warmthSlider.onValueChanged.RemoveListener(OnSliderChanged);
        if (riskToleranceSlider != null) riskToleranceSlider.onValueChanged.RemoveListener(OnSliderChanged);
        if (formalitySlider != null) formalitySlider.onValueChanged.RemoveListener(OnSliderChanged);
        if (verbositySlider != null) verbositySlider.onValueChanged.RemoveListener(OnSliderChanged);

        UnbindAnimal();
    }

    private void OnSliderChanged(float _)
    {
        UpdateValueTexts();
    }

    private void SetupSlider(Slider s)
    {
        if (s == null) return;
        s.minValue = 0f;
        s.maxValue = 2f;
        s.wholeNumbers = true;
        s.value = Mathf.Clamp(s.value, 0f, 2f);
    }

    private int ReadSliderInt(Slider s, int fallback)
    {
        if (s == null) return fallback;
        return Mathf.Clamp(Mathf.RoundToInt(s.value), 0, 2);
    }

    private void UpdateValueTexts()
    {
        int w = ReadSliderInt(warmthSlider, 1);
        int r = ReadSliderInt(riskToleranceSlider, 0);
        int f = ReadSliderInt(formalitySlider, 1);
        int v = ReadSliderInt(verbositySlider, 1);

        if (warmthValueText != null) warmthValueText.text = w.ToString();
        if (riskValueText != null) riskValueText.text = r.ToString();
        if (formalityValueText != null) formalityValueText.text = f.ToString();
        if (verbosityValueText != null) verbosityValueText.text = v.ToString();
    }

    // 외부(SessionManager 등)에서 동물이 바뀔 때 호출해도 된다
    public void BindAnimal(AnimalAgent agent)
    {
        if (currentAnimal == agent) return;

        UnbindAnimal();
        currentAnimal = agent;

        if (currentAnimal == null) return;

        currentAnimal.OnPlayerAsked += HandlePlayerAsked;
        currentAnimal.OnReply += HandleAnimalReply;

        ClearLines();
    }

    private void UnbindAnimal()
    {
        if (currentAnimal == null) return;

        currentAnimal.OnPlayerAsked -= HandlePlayerAsked;
        currentAnimal.OnReply -= HandleAnimalReply;
    }

    private void HandlePlayerAsked(string text)
    {
        AddLine("플레이어", text);
    }

    private void HandleAnimalReply(AnimalReply reply)
    {
        if (string.IsNullOrWhiteSpace(reply.response)) return;
        AddLine("동물", reply.response);
    }

    private void AddLine(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        text = text.Replace("\n", " ").Replace("\r", " ").Trim();
        if (text.Length > 180) text = text.Substring(0, 180).Trim();

        lines.Add(new Line { speaker = speaker, text = text });

        if (lines.Count > maxStoredLines)
            lines.RemoveAt(0);
    }

    private void ClearLines()
    {
        lines.Clear();
    }

    private string BuildRecentTranscript(int maxLines)
    {
        if (maxLines <= 0) return "";

        int start = Mathf.Max(0, lines.Count - maxLines);
        var sb = new StringBuilder();

        for (int i = start; i < lines.Count; i++)
        {
            sb.Append(lines[i].speaker);
            sb.Append(": ");
            sb.Append(lines[i].text);
            sb.Append("\n");
        }

        return sb.ToString().Trim();
    }

    public async void OnClickA() { await GenerateAndFill(ActionMethod.SAFETY); }
    public async void OnClickB() { await GenerateAndFill(ActionMethod.OBSERVE); }
    public async void OnClickC() { await GenerateAndFill(ActionMethod.PROBE); }
    public async void OnClickD() { await GenerateAndFill(ActionMethod.OFFER_CLOSE); }

    public async void OnClickReroll()
    {
        await GenerateAndFill(lastMethod);
    }

    private async Task GenerateAndFill(ActionMethod method)
    {
        if (isGenerating) return;
        if (chatService == null || inputField == null) return;

        isGenerating = true;
        lastMethod = method;

        int warmth = ReadSliderInt(warmthSlider, 1);
        int riskTolerance = ReadSliderInt(riskToleranceSlider, 0);
        int formality = ReadSliderInt(formalitySlider, 1);
        int verbosity = ReadSliderInt(verbositySlider, 1);

        string animalName = "동물";
        string animalPersonality = "";
        string animalStatus = "";

        if (currentAnimal != null && currentAnimal.Species != null)
        {
            var species = currentAnimal.Species;

            animalName = string.IsNullOrWhiteSpace(species.displayName) ? "동물" : species.displayName;

            // enum -> string
            animalPersonality = species.personality.ToString();
            animalStatus = species.baseStatus.ToString();
        }

        string transcript = BuildRecentTranscript(recentContextLines);

        string systemPrompt =
            "너는 게임에서 플레이어가 말할 다음 한 문장을 생성한다.\n" +
            "반드시 한국어 한 문장만 출력한다.\n" +
            "줄바꿈 금지, 따옴표 금지, JSON 금지, 설명 금지.\n" +
            "이모지 및 특수기호 사용 금지.\n" +
            "대화 기록에 없는 사실을 만들어내지 말고, 바로 직전 흐름에 자연스럽게 이어져야 한다.\n" +
            "\n" +
            "Action 정의:\n" +
            "SAFETY: 압박하지 않고 배려하며 안전감을 주는 말.\n" +
            "OBSERVE: 관찰 기반으로 현재 상태를 짚고 확인하는 말.\n" +
            "PROBE: 더 알아내기 위한 질문. riskTolerance가 높을수록 직설적이어도 된다.\n" +
            "OFFER_CLOSE: 부담을 낮추며 마무리하거나 선택지를 주는 말.\n" +
            "\n" +
            "스타일 파라미터:\n" +
            "- warmth(0~2): 높을수록 배려, 완충 표현 증가\n" +
            "- riskTolerance(0~2): 높을수록 직설, 떠보기 질문 허용\n" +
            "- formality(0~2): 0은 무례하고 거칠게, 1은 기본, 2는 더 정중\n" +
            "- verbosity(0~2): 0은 아주 짧게, 1은 보통, 2는 길게(그래도 한 문장)\n";

        string userPrompt =
            "상황:\n" +
            "- 플레이어는 야생 동물을 보호소로 설득하려는 직원이다.\n" +
            "- 대화 상대: " + animalName + "\n" +
            "- 성격: " + animalPersonality + "\n" +
            "- 상태: " + animalStatus + "\n" +
            "\n" +
            "최근 대화 기록:\n" + (string.IsNullOrWhiteSpace(transcript) ? "(없음)" : transcript) + "\n" +
            "\n" +
            "이번에 생성할 조건:\n" +
            "action=" + method +
            ", warmth=" + warmth +
            ", riskTolerance=" + riskTolerance +
            ", formality=" + formality +
            ", verbosity=" + verbosity + "\n" +
            "\n" +
            "대화 흐름을 이어서, 플레이어의 다음 발화 한 문장만 출력해라.";

        try
        {
            inputField.text = "문장 생성 중...";
            // 중요: OneShot을 써야 AnimalAgent의 대화 컨텍스트를 건드리지 않는다
            string result = await chatService.GetOneShotAsync(systemPrompt, userPrompt);

            result = SanitizeOneLine(result);

            if (string.IsNullOrWhiteSpace(result))
                result = "잠깐만, 다시 말해줄래?";

            inputField.text = result;
            inputField.ActivateInputField();
            inputField.MoveTextEnd(false);
        }
        finally
        {
            isGenerating = false;
        }
    }

    private string SanitizeOneLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        s = s.Trim();

        int idx = s.IndexOf('\n');
        if (idx >= 0)
            s = s.Substring(0, idx).Trim();

        if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            s = s.Substring(1, s.Length - 2).Trim();

        // 흔히 튀는 기호들 최소 정리(완전한 필터는 아님)
        s = s.Replace("•", "");
        s = s.Replace("※", "");
        s = s.Replace("★", "");
        s = s.Replace("☆", "");

        return s.Trim();
    }
}
