using UnityEngine;
using UnityEngine.SceneManagement;

public class StageSelection : MonoBehaviour
{
    private static StageSelection instance;

    // 테스트 단계: Stage2를 기본으로
    public static int SelectedStageId { get; private set; } = 2;

    [Header("Defaults")]
    [SerializeField] private int defaultStageId = 2;

    private bool isLoading;

    public static StageSelection Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<StageSelection>();
                if (instance == null)
                {
                    var go = new GameObject("StageSelection");
                    instance = go.AddComponent<StageSelection>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 저장값이 있으면 반영, 없으면 Stage2(defaultStageId)
        int stageId = defaultStageId;
        var save = SaveManager.Instance;
        if (save != null && save.Data != null && save.Data.lastSelectedStageId > 0)
            stageId = save.Data.lastSelectedStageId;

        // 테스트용: 유효하지 않으면 Stage2로 강제
        if (stageId <= 0) stageId = defaultStageId;

        SelectedStageId = stageId;
    }

    public static void SetSelectedStageId(int stageId)
    {
        // 테스트 단계: 잘못된 값이면 Stage2로
        if (stageId <= 0) stageId = 2;

        SelectedStageId = stageId;

        var save = SaveManager.Instance;
        if (save != null && save.Data != null)
        {
            save.Data.lastSelectedStageId = stageId;
            save.Save();
        }
    }

    public static int GetSelectedStageIdOrDefault()
    {
        if (SelectedStageId <= 0) return 2;
        return SelectedStageId;
    }

    // 전역 씬 로더 (어느 씬에서나 사용)
    public static void LoadSceneByName(string sceneName)
    {
        Instance.LoadSceneByNameInstance(sceneName);
    }

    public static void LoadSceneByIndex(int buildIndex)
    {
        Instance.LoadSceneByIndexInstance(buildIndex);
    }

    public static void ReloadCurrentScene()
    {
        Instance.ReloadCurrentSceneInstance();
    }

    private void LoadSceneByNameInstance(string sceneName)
    {
        if (isLoading) return;
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        isLoading = true;
        SceneManager.LoadScene(sceneName);
        isLoading = false;
    }

    private void LoadSceneByIndexInstance(int buildIndex)
    {
        if (isLoading) return;
        if (buildIndex < 0) return;

        isLoading = true;
        SceneManager.LoadScene(buildIndex);
        isLoading = false;
    }

    private void ReloadCurrentSceneInstance()
    {
        if (isLoading) return;

        isLoading = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        isLoading = false;
    }
}
