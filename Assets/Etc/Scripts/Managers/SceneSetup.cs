using UnityEngine;

public class SceneSetup : MonoBehaviour
{
    [SerializeField] private AnimalAgent targetAnimal;

    [Header("스테이지 설정(인스펙터 주입)")]
    [SerializeField] private StageConfigSO stage1Config;
    [SerializeField] private StageConfigSO stage2Config;
    [SerializeField] private StageConfigSO stage3Config;
    [SerializeField] private int defaultStageId = 2;

    private void Start()
    {
        var session = GetComponent<SessionManager>();

        int stageId = defaultStageId;

        if (StageSelection.SelectedStageId > 0)
        {
            stageId = StageSelection.SelectedStageId;
        }
        else
        {
            var save = SaveManager.Instance;
            if (save != null && save.Data != null && save.Data.lastSelectedStageId > 0)
                stageId = save.Data.lastSelectedStageId;
        }

        // Stage1 완료 후에는 Stage1을 강제로 막음(예외 상황 대비)
        var sm = SaveManager.Instance;
        if (sm != null && sm.IsStage1Completed() && stageId == 1)
        {
            if (sm.IsStage3Unlocked()) stageId = 3;
            else if (sm.IsStage2Unlocked()) stageId = 2;
        }

        StageConfigSO selectedConfig = stage1Config;
        if (stageId == 2) selectedConfig = stage2Config;
        else if (stageId == 3) selectedConfig = stage3Config;

        if (session != null) session.SetStageConfig(selectedConfig);

        var runQueue = Object.FindFirstObjectByType<AnimalRunQueue>();
        if (runQueue != null)
        {
            runQueue.StartRun();
            bool spawned = runQueue.SpawnNext();
            if (spawned) return;
        }

        var chat = Object.FindFirstObjectByType<ChatServiceOpenAI>();

        if (targetAnimal == null)
            targetAnimal = Object.FindFirstObjectByType<AnimalAgent>();

        if (session == null || chat == null || targetAnimal == null) return;

        targetAnimal.ChatService = chat;
        session.StartSession(targetAnimal);

        var nameLabel = targetAnimal.GetComponentInParent<AnimalNameLabel>();
        if (nameLabel != null)
            nameLabel.RefreshName();
    }
}
