using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StageSelectUI : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField] private Button startButton;
    [Tooltip("이어하기 버튼 (저장된 데이터가 있을 때만 활성화)")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button encyclopediaButton;
    [SerializeField] private Button optionButton;
    [SerializeField] private Button exitButton;

    [Header("Panels (Optional)")]
    public Canvas defaultCanvas;
    [SerializeField] private Canvas selectStageCanvas;
    [SerializeField] private Canvas encyclopediaCanvas;
    [SerializeField] private Canvas issueCanvas;
    [Tooltip("인스펙터에서 LanguageUI로 연결")]
    [SerializeField] private Canvas optionCanvas;   // languageUI로 연결
    [SerializeField] private Canvas exitCanvas;

    [Header("Encyclopedia Camera Focus")]
    public GameObject animals;
    [Tooltip("0,1,2,3 중 하나")]
    [SerializeField] private int index = 0;
    [SerializeField] private Camera targetCamera;
    [Tooltip("이동에 걸리는 시간(초)")]
    [SerializeField] private float moveDuration = 0.35f;
    private Coroutine moveCo;

    [Header("Button Settings")]
    [SerializeField] private float disabledAlpha = 0.3f;

    public EncyclopediaManager encyclopediaManager;

    [Header("Stage2 Only (Test)")]
    [SerializeField] private Button stage2Button;
    [SerializeField] private string stage2SceneName = "Stage2";

    private void Awake()
    {
        // StageSelection이 씬에 없어도 전역 생성되도록 보장
        _ = StageSelection.Instance;        

        animals.SetActive(false);
        ToggleCanvas(defaultCanvas, true);
        ToggleCanvas(encyclopediaCanvas, false);
        // ToggleCanvas(issueCanvas, false);

        // 1. startButton: 이제 '새로 시작' 기능을 직접 수행합니다.
        if (startButton != null)
            startButton.onClick.AddListener(OnClickNewRun);

        // 2. continueButton: 저장된 데이터로 이어하기를 수행합니다.
        if (continueButton != null)
            continueButton.onClick.AddListener(OnClickContinue);

        if (encyclopediaButton != null)
        {
            encyclopediaButton.onClick.AddListener(() => ToggleCanvas(encyclopediaCanvas, true));
            encyclopediaButton.onClick.AddListener(() => ToggleCanvas(defaultCanvas, false));
            encyclopediaButton.onClick.AddListener(() => SetActiveAnimals(true));
            encyclopediaButton.onClick.AddListener(() => MoveCameraFocusByIndex(index));
            encyclopediaButton.onClick.AddListener(encyclopediaManager.RefreshAll);
        }
        if (optionButton != null) optionButton.onClick.AddListener(() => ToggleCanvas(optionCanvas, true));
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(() => ToggleCanvas(exitCanvas, true));            
        }
        if (stage2Button != null) stage2Button.onClick.AddListener(OnClickStage2);
    }

    void Start()
    {
        // 시작 시 이어하기 버튼의 활성 상태를 체크합니다.
        UpdateContinueButtonState();
    }

    private void SetActiveAnimals(bool on)
    {
        animals.SetActive(on);
    }

    private void ToggleCanvas(Canvas canvas, bool on)
    {
        if (canvas == null) return;
        canvas.gameObject.SetActive(on);
    }

    // 패널 닫기용 (버튼 OnClick에 연결 가능)
    public void CloseDefault() => ToggleCanvas(defaultCanvas, false);
    public void CloseSelectStage() => ToggleCanvas(selectStageCanvas, false);
    public void CloseEncyclopedia()
    {
        // 도감 UI 끄기
        ToggleCanvas(encyclopediaCanvas, false);

        // 도감 동물 오브젝트들 끄기
        if (animals != null)
            animals.SetActive(false);

        // 기본 UI 다시 켜기(원하는 흐름이면)
        ToggleCanvas(defaultCanvas, true);

        // (선택) 도감에서 카메라 이동 중이면 중단
        if (moveCo != null)
        {
            StopCoroutine(moveCo);
            moveCo = null;
        }

        // (선택) 카메라 초점을 기본 그룹(0)으로 되돌리고 싶다면:
        // index = 0;
        MoveCameraFocusByIndex(-1);
    }
    public void CloseOption() => ToggleCanvas(optionCanvas, false);
    public void CloseExit() => ToggleCanvas(exitCanvas, false);

    private void OnClickStage2()
    {
        // 테스트 단계: Stage2가 디폴트
        StageSelection.SetSelectedStageId(2);

        if (string.IsNullOrWhiteSpace(stage2SceneName))
        {
            Debug.LogWarning("[StageSelectUI] stage2SceneName is empty.");
            return;
        }

        StageSelection.LoadSceneByName(stage2SceneName);
    }

    // 어느 씬에서든 버튼 OnClick에 바로 연결해서 사용 가능
    public void LoadSceneByName(string sceneName)
    {
        StageSelection.LoadSceneByName(sceneName);
    }

    public void ReloadCurrentScene()
    {
        StageSelection.ReloadCurrentScene();
    }




    private void OnClickEncyclopedia(bool isOn)
    {

        MoveCameraFocusByIndex(index);
        if (isOn)
        {
            ToggleCanvas(defaultCanvas, !isOn);
            ToggleCanvas(encyclopediaCanvas, isOn);
            animals.SetActive(isOn);
        }            
        else
        {
            ToggleCanvas(encyclopediaCanvas, isOn);
            ToggleCanvas(defaultCanvas, !isOn);
            animals.SetActive(!isOn);
        }
    }

    public void MoveCameraFocusByIndex(int newIndex)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null) return;



        int clamped = Mathf.Clamp(newIndex, 0, 3);
        index = clamped;

        Vector2 xy = GetFocusXY(clamped);
        Vector3 startPos = targetCamera.transform.position;
        Vector3 endPos = new Vector3(xy.x, xy.y, startPos.z);        
        
        if (moveCo != null)
            StopCoroutine(moveCo);

        if (newIndex == -1)
            moveCo = StartCoroutine(CoMoveCamera(startPos, new Vector3(0, 0, startPos.z), moveDuration));
        else
            moveCo = StartCoroutine(CoMoveCamera(startPos, endPos, moveDuration));
    }

    private IEnumerator CoMoveCamera(Vector3 startPos, Vector3 endPos, float duration)
    {
        if (duration <= 0f)
        {
            targetCamera.transform.position = endPos;
            moveCo = null;
            yield break;
        }

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            // 부드러운 보간(가속/감속)
            float eased = Mathf.SmoothStep(0f, 1f, t);

            targetCamera.transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        targetCamera.transform.position = endPos;
        moveCo = null;
    }

    private Vector2 GetFocusXY(int idx)
    {
        switch (idx)
        {
            case 0: return new Vector2(-15f, 15f);
            case 1: return new Vector2(-5f, 15f);
            case 2: return new Vector2(5f, 15f);
            case 3: return new Vector2(15f, 15f);
            default: return new Vector2(-15f, 15f);
        }
    }

    public void ShowGroup(bool isRight)
    {
        if(isRight)
            index++;
        else
            index--;
        index = (index + 4) % 4;
        MoveCameraFocusByIndex(index);
    }





    // 이어하기 버튼의 활성화/투명도를 제어합니다.
    private void UpdateContinueButtonState()
    {
        if (continueButton == null) return;

        // SaveManager에서 저장된 런이 있는지 확인
        bool hasRun = SaveManager.Instance.Data.hasSavedRun;
        continueButton.interactable = hasRun;

        // CanvasGroup이 있다면 투명도 조절, 없으면 Image 색상 조절
        CanvasGroup cg = continueButton.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = hasRun ? 1.0f : disabledAlpha;
        }
        else
        {
            Image img = continueButton.targetGraphic as Image;
            if (img != null)
            {
                Color c = img.color;
                c.a = hasRun ? 1.0f : disabledAlpha;
                img.color = c;
            }
        }
    }

    // [새로 시작] 기존 데이터를 지우고 씬 로드
    private void OnClickNewRun()
    {
        Debug.Log("🚨🚨 [StageSelectUI] '새로 시작' 버튼 눌림! 세이브 데이터를 지웁니다! 🚨🚨");
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Data.hasSavedRun = false;
            SaveManager.Instance.Data.savedRunOrderIds.Clear();
            SaveManager.Instance.Save();
        }
        LoadStage2();
    }

    // [이어하기] 저장된 데이터 유지하며 씬 로드
    private void OnClickContinue()
    {
        Debug.Log("✅✅ [StageSelectUI] '이어하기' 버튼 눌림! 데이터를 유지하고 진입합니다! ✅✅");
        LoadStage2();
    }

    private void LoadStage2()
    {
        StageSelection.SetSelectedStageId(2);
        StageSelection.LoadSceneByName(stage2SceneName);
    }










    public void QuitGame()
    {
        // 유니티 에디터에서 테스트할 때도 멈추도록 설정합니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 빌드된 게임(PC/모바일 등)에서 실제로 종료합니다.
        Application.Quit();
#endif
    }
}
