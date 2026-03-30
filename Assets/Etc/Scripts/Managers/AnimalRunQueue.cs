using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AnimalRunQueue : MonoBehaviour
{
    [Header("SO 로드 경로(Resources)")]
    [SerializeField] private string speciesResourcesPath = "MyAnimals/SpeciesSO";

    [Header("스폰")]
    [SerializeField] private GameObject animalPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("의존성")]
    [SerializeField] private SessionManager session;
    [SerializeField] private ChatServiceOpenAI chat;
    [SerializeField] private SaveManager save;

    [Header("런 설정")]
    [SerializeField] private int daysPerRun = 7;
    [SerializeField] private bool autoSpawnNextOnSessionEnd = true;

    [Header("다음 동물 전환 딜레이")]
    [SerializeField] private float nextAnimalDelaySeconds = 1.5f;

    [Header("런 완료 시 씬 이동")]
    [SerializeField] private bool loadMainSceneWhenRunCompleted = true;
    [SerializeField] private string mainSceneName = "Main";

    [Header("Registry")]
    [SerializeField] private SpeciesRegistrySO registry; // 인스펙터에서 등록

    private readonly List<AnimalSpeciesSO> runOrder = new List<AnimalSpeciesSO>();
    private int dayIndex;
    private int runSuccessCount;

    private AnimalAgent spawnedAnimal;
    private string lastLoadedPath = "";
    private Coroutine spawnNextCoroutine;
    private readonly List<int> runDayResults = new List<int>();

    private void Awake() => EnsureDependencies();

    private void OnEnable()
    {
        EnsureDependencies();
        if (session != null) session.OnSessionEnded += HandleSessionEnded;
    }

    private void OnDisable()
    {
        if (session != null) session.OnSessionEnded -= HandleSessionEnded;
        if (spawnNextCoroutine != null) StopCoroutine(spawnNextCoroutine);
    }

    private void EnsureDependencies()
    {
        if (session == null) session = Object.FindFirstObjectByType<SessionManager>();
        if (chat == null) chat = Object.FindFirstObjectByType<ChatServiceOpenAI>();
        if (save == null) save = SaveManager.Instance;
    }

    private int GetDaysPerRun() => (session != null && session.StageConfig != null) ? session.StageConfig.daysPerRun : daysPerRun;

    // [핵심 수정] 게임 시작 시 호출되는 진입점
    public void StartRun()
    {
        save = SaveManager.Instance;
        EnsureDependencies();

        int currentStageId = (session != null && session.StageConfig != null) ? session.StageConfig.stageId : 0;

        // [추가됨] 어떤 데이터 때문에 이어하기가 거부되는지 원인을 콘솔에 띄웁니다.
        Debug.Log($"[AnimalRunQueue] 체크 -> hasSavedRun: {save.Data.hasSavedRun}, savedStageId: {save.Data.savedStageId}, currentStageId: {currentStageId}");

        // 1. 저장된 런 데이터가 있는지 우선 확인
        if (save != null && save.Data.hasSavedRun)
        {
            // 스테이지 아이디가 미묘하게 꼬여도 강제로 불러오도록 조건을 유연하게 바꿨습니다.
            if (save.Data.savedStageId == currentStageId || save.Data.savedStageId == 0 || currentStageId == 0)
            {
                LoadSavedRun();

                // 성공적으로 동물을 불러왔다면 여기서 무조건 종료 (NewRun 실행 완벽 차단)
                if (runOrder.Count > 0)
                {
                    return;
                }
            }
            else
            {
                Debug.LogWarning($"[AnimalRunQueue] 스테이지 ID가 달라서 이어하기가 취소되었습니다. (저장됨: {save.Data.savedStageId}, 현재: {currentStageId})");
            }
        }

        // 2. 세이브가 없거나 실패했을 때만 새로운 런 시작
        NewRun(currentStageId);
    }

    private void LoadSavedRun()
    {
        runOrder.Clear();

        if (registry != null && registry.allSpecies.Count > 0)
        {
            foreach (string id in save.Data.savedRunOrderIds)
            {
                var found = registry.GetSpeciesById(id);
                if (found != null) runOrder.Add(found);
            }
        }

        if (runOrder.Count == 0)
        {
            Debug.LogWarning("[AnimalRunQueue] 레지스트리에서 동물을 찾지 못해 Resources 폴더를 검색합니다.");
            string stagePath = GetStageScopedResourcesPath();
            var loaded = Resources.LoadAll<AnimalSpeciesSO>(stagePath);

            if ((loaded == null || loaded.Length == 0) && stagePath != speciesResourcesPath)
            {
                loaded = Resources.LoadAll<AnimalSpeciesSO>(speciesResourcesPath);
            }

            if (loaded != null)
            {
                foreach (string id in save.Data.savedRunOrderIds)
                {
                    foreach (var so in loaded)
                    {
                        if (so != null && so.id == id)
                        {
                            runOrder.Add(so);
                            break;
                        }
                    }
                }
            }
        }

        dayIndex = save.Data.savedDayIndex;
        runSuccessCount = save.Data.savedRunSuccessCount;
        RestoreRunDayResultsFromSave();

        Debug.Log($"[AnimalRunQueue] 이어하기 준비 완료: {runOrder.Count}종 복구, {dayIndex + 1}일차부터 시작.");
    }

    private void NewRun(int stageId)
    {
        LoadAllSpeciesFromResources();

        if (runOrder.Count == 0) return;

        runOrder.RemoveAll(s =>
            s == null || string.IsNullOrWhiteSpace(s.id) ||
            (stageId == 1 && save.IsStage1SpeciesCleared(s.id)) ||
            (stageId == 2 && save.IsStage2SpeciesCleared(s.id))
        );

        Shuffle(runOrder);
        dayIndex = 0;
        runSuccessCount = 0;
        ResetRunDayResults(GetDaysPerRun());

        Debug.Log($"[AnimalRunQueue] 새로운 런을 시작합니다. (대상: {runOrder.Count}종)");
    }

    // [중단하기] 현재 진행 상태를 저장 (SessionUI의 버튼에서 호출)
    public void SaveRunProgress()
    {
        save = SaveManager.Instance;

        if (save == null || runOrder.Count == 0) return;

        save.Data.hasSavedRun = true;
        save.Data.savedStageId = (session != null && session.StageConfig != null) ? session.StageConfig.stageId : 0;

        save.Data.savedDayIndex = Mathf.Max(0, dayIndex - 1);
        save.Data.savedRunSuccessCount = runSuccessCount;

        save.Data.savedRunOrderIds.Clear();
        foreach (var species in runOrder)
        {
            save.Data.savedRunOrderIds.Add(species.id);
        }

        save.Data.savedRunDayResults.Clear();
        save.Data.savedRunDayResults.AddRange(runDayResults);

        save.Save();
        Debug.Log($"[AnimalRunQueue] 저장 완료: {save.Data.savedDayIndex + 1}일차 동물부터 재개 예정.");
    }

    // [다시하기] 저장된 진행 데이터를 삭제 (SessionUI의 버튼에서 호출)
    public void ClearRunProgress()
    {
        save = SaveManager.Instance;
        if (save == null) return;

        save.Data.hasSavedRun = false;
        save.Data.savedRunOrderIds.Clear();
        save.Data.savedRunDayResults.Clear();

        save.Save();
        Debug.Log("[AnimalRunQueue] 데이터가 초기화되었습니다.");
    }

    // [추가] UI 등 다른 스크립트에서 과거의 동물 데이터를 읽어갈 수 있도록 돕는 메서드
    public AnimalSpeciesSO GetRunSpeciesAt(int index)
    {
        if (index >= 0 && index < runOrder.Count)
            return runOrder[index];
        return null;
    }




    private void ResetRunDayResults(int count)
    {
        runDayResults.Clear();

        for (int i = 0; i < count; i++)
            runDayResults.Add(0);
    }

    private void RestoreRunDayResultsFromSave()
    {
        runDayResults.Clear();

        if (save != null && save.Data.savedRunDayResults != null)
            runDayResults.AddRange(save.Data.savedRunDayResults);

        int targetCount = GetDaysPerRun();

        while (runDayResults.Count < targetCount)
            runDayResults.Add(0);

        if (runDayResults.Count > targetCount)
            runDayResults.RemoveRange(targetCount, runDayResults.Count - targetCount);
    }

    public int GetRunDayResultAt(int index)
    {
        if (index < 0 || index >= runDayResults.Count)
            return 0;

        return runDayResults[index];
    }






    public bool SpawnNext()
    {
        EnsureDependencies();

        if (session == null || chat == null || animalPrefab == null || runOrder.Count == 0) return false;

        int days = GetDaysPerRun();
        if (dayIndex >= days || dayIndex >= runOrder.Count) return false;

        if (spawnedAnimal != null) Destroy(spawnedAnimal.gameObject);

        AnimalSpeciesSO species = runOrder[dayIndex];
        Vector3 pos = (spawnPoint != null) ? spawnPoint.position : Vector3.zero;
        GameObject go = Instantiate(animalPrefab, pos, Quaternion.identity);

        var animal = go.GetComponentInChildren<AnimalAgent>(true);
        if (animal == null) { Destroy(go); return false; }

        animal.ChatService = chat;
        animal.Init(species);

        var nameLabel = go.GetComponentInChildren<AnimalNameLabel>(true);
        if (nameLabel != null) nameLabel.RefreshName();

        int currentDay = dayIndex + 1;
        int animalsToWinRun = (session.StageConfig != null) ? session.StageConfig.animalsToWinRun : 0;

        session.SetRunContext(currentDay, days, runSuccessCount, animalsToWinRun);
        session.SetCurrentSpecies(species);

        animal.BindSession(session);
        session.StartSession(animal);

        spawnedAnimal = animal;
        dayIndex++; // 다음 스폰을 위해 인덱스 증가

        return true;
    }

    private void HandleSessionEnded(bool success, int heartOpenCount)
    {
        int completedDayIndex = dayIndex - 1;
        if (completedDayIndex >= 0 && completedDayIndex < runDayResults.Count)
            runDayResults[completedDayIndex] = success ? 1 : 2;

        if (success) runSuccessCount++;

        if (success && session.StageConfig != null && spawnedAnimal != null && save != null)
        {
            save.MarkSpeciesCleared(session.StageConfig.stageId, spawnedAnimal.Species.id, session.StageConfig.totalSpeciesCount);
        }

        if (session.StageConfig != null)
        {
            session.SetRunContext(dayIndex, GetDaysPerRun(), runSuccessCount, session.StageConfig.animalsToWinRun);
        }

        if (!autoSpawnNextOnSessionEnd) return;

        int totalDays = GetDaysPerRun();
        bool runCompleted = (dayIndex >= totalDays) || (dayIndex >= runOrder.Count);

        if (runCompleted)
        {
            ClearRunProgress();

            if (loadMainSceneWhenRunCompleted && !string.IsNullOrWhiteSpace(mainSceneName))
            {
                if (StageSelection.Instance != null) StageSelection.LoadSceneByName(mainSceneName);
                else SceneManager.LoadScene(mainSceneName);
            }
            return;
        }

        StartSpawnNextAfterDelay();
    }

    private void StartSpawnNextAfterDelay()
    {
        if (spawnNextCoroutine != null) StopCoroutine(spawnNextCoroutine);
        spawnNextCoroutine = StartCoroutine(SpawnNextAfterDelayRoutine());
    }

    private IEnumerator SpawnNextAfterDelayRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, nextAnimalDelaySeconds));
        SpawnNext();
        spawnNextCoroutine = null;
    }

    private void LoadAllSpeciesFromResources()
    {
        runOrder.Clear();
        string stagePath = GetStageScopedResourcesPath();
        var loaded = Resources.LoadAll<AnimalSpeciesSO>(stagePath);

        if ((loaded == null || loaded.Length == 0) && stagePath != speciesResourcesPath)
        {
            stagePath = speciesResourcesPath;
            loaded = Resources.LoadAll<AnimalSpeciesSO>(stagePath);
        }

        if (loaded != null)
        {
            foreach (var so in loaded) if (so != null) runOrder.Add(so);
        }
    }

    private string GetStageScopedResourcesPath()
    {
        string root = (speciesResourcesPath ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(root)) root = "MyAnimals/SpeciesSO";
        int stageId = (session != null && session.StageConfig != null) ? session.StageConfig.stageId : 0;
        return stageId <= 0 ? root : $"{root}/Stage{stageId}";
    }

    private void Shuffle(List<AnimalSpeciesSO> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}