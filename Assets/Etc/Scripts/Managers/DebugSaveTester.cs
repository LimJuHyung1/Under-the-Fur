using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 추가
using System.Collections.Generic;

public class DebugSaveTester : MonoBehaviour
{
    [Header("의존성 연결")]
    [SerializeField] private AnimalRunQueue runQueue;
    [SerializeField] private SessionManager sessionManager;
    [SerializeField] private string mainSceneName = "Main"; // 이동할 메인 씬 이름

    private void Awake()
    {
        if (runQueue == null) runQueue = FindFirstObjectByType<AnimalRunQueue>();
        if (sessionManager == null) sessionManager = FindFirstObjectByType<SessionManager>();
    }

    public void Debug_SkipToNextDaySuccess() => ProcessSkip(true);
    public void Debug_SkipToNextDayFailure() => ProcessSkip(false);

    private void ProcessSkip(bool isSuccess)
    {
        if (runQueue == null || sessionManager == null) return;

        // 1. 현재 세션 결과 기록
        if (isSuccess && sessionManager.CurrentSpecies != null)
        {
            int stageId = (sessionManager.StageConfig != null) ? sessionManager.StageConfig.stageId : 2;
            int total = (sessionManager.StageConfig != null) ? sessionManager.StageConfig.totalSpeciesCount : 55;
            SaveManager.Instance.MarkSpeciesCleared(stageId, sessionManager.CurrentSpecies.id, total);
        }

        // 2. 다음 날 스폰 시도
        bool hasNext = runQueue.SpawnNext();

        // 3. 더 이상 스폰할 동물이 없다면(7일 완료) 메인 씬으로 이동
        if (!hasNext)
        {
            Debug.Log("<color=magenta>[Debug] 7일간의 런이 모두 완료되었습니다. 메인 씬으로 이동합니다.</color>");
            LoadMainScene();
        }
    }

    private void LoadMainScene()
    {
        // AnimalRunQueue에 설정된 메인 씬 이름을 우선 사용합니다
        string sceneToLoad = mainSceneName;

        // 씬 로드 실행
        SceneManager.LoadScene(sceneToLoad);
    }
}