using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    private static SaveManager instance;
    public static SaveManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SaveManager>();
                if (instance == null)
                {
                    var go = new GameObject("SaveManager");
                    instance = go.AddComponent<SaveManager>();
                }
            }
            return instance;
        }
    }

    [SerializeField] private bool logSavePathOnLoad = true;

    private const string FileName = "shelter_talk_save.json";

    private SaveData data = new SaveData();
    private readonly HashSet<string> stage1ClearedSet = new HashSet<string>();
    private readonly HashSet<string> stage2ClearedSet = new HashSet<string>();

    public SaveData Data => data;
    private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // [핵심 해결책] SaveManager가 다른 오브젝트의 자식으로 있으면 DontDestroyOnLoad가 무시됩니다.
        // 씬 전환 시 파괴되는 것을 막기 위해 강제로 하이어라키 최상위로 빼냅니다.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Load();

        if (logSavePathOnLoad)
            LogSaveFilePath();
    }

    public void Load()
    {
        data = new SaveData();
        stage1ClearedSet.Clear();

        try
        {
            if (!File.Exists(FilePath))
            {
                Save();
                return;
            }

            string json = File.ReadAllText(FilePath);
            // [추가된 디버그] 디스크에서 어떤 값을 읽어왔는지 텍스트로 바로 확인합니다.
            Debug.Log($"[SaveManager] 📂 물리 드라이브에서 불러온 JSON 내용:\n{json}");

            if (string.IsNullOrWhiteSpace(json))
            {
                Save();
                return;
            }

            var loaded = JsonUtility.FromJson<SaveData>(json);
            if (loaded == null)
            {
                Save();
                return;
            }

            data = loaded;
            // [추가된 디버그] 데이터가 파싱된 후의 상태를 확인합니다.
            Debug.Log($"[SaveManager] 📂 데이터 로드 성공! hasSavedRun 최종 상태: {data.hasSavedRun}");

            // 구버전 호환
            if (data.stage2Unlocked && !data.stage1Completed)
                data.stage1Completed = true;

            if (data.stage1ClearedSpeciesIds != null)
            {
                for (int i = 0; i < data.stage1ClearedSpeciesIds.Count; i++)
                {
                    string id = data.stage1ClearedSpeciesIds[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        stage1ClearedSet.Add(id);
                }
            }

            if (data.stage2ClearedSpeciesIds != null)
            {
                for (int i = 0; i < data.stage2ClearedSpeciesIds.Count; i++)
                {
                    string id = data.stage2ClearedSpeciesIds[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        stage2ClearedSet.Add(id);
                }
            }

            SyncListFromSet();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] Load failed. Reset to default. " + e.Message);
            data = new SaveData();
            stage1ClearedSet.Clear();
            Save();
        }
    }

    public void Save()
    {
        try
        {
            SyncListFromSet();

            string json = JsonUtility.ToJson(data, true);

            // [추가된 디버그] 디스크에 쓰기 직전의 JSON 상태를 확인합니다.
            Debug.Log($"[SaveManager] 💾 디스크에 저장하는 JSON 내용:\n{json}");

            string dir = Application.persistentDataPath;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] Save failed: " + e.Message);
        }
    }

    private void SyncListFromSet()
    {
        if (data.stage1ClearedSpeciesIds == null)
            data.stage1ClearedSpeciesIds = new List<string>();

        data.stage1ClearedSpeciesIds.Clear();
        foreach (var id in stage1ClearedSet)
            data.stage1ClearedSpeciesIds.Add(id);

        // Stage2 동기화
        if (data.stage2ClearedSpeciesIds == null)
            data.stage2ClearedSpeciesIds = new List<string>();

        data.stage2ClearedSpeciesIds.Clear();
        foreach (var id in stage2ClearedSet)
            data.stage2ClearedSpeciesIds.Add(id);
    }

    // Stage1 클리어 기록
    // Stage1 클리어 기록
    public void MarkSpeciesCleared(int stageId, string speciesId, int stageTotalSpeciesCount)
    {
        if (string.IsNullOrWhiteSpace(speciesId)) return;

        bool added = false;
        bool changed = false;

        if (stageId == 1)
        {
            added = stage1ClearedSet.Add(speciesId);

            // Stage1 완료 판정
            if (stageTotalSpeciesCount > 0 && stage1ClearedSet.Count >= stageTotalSpeciesCount)
            {
                if (!data.stage1Completed)
                {
                    data.stage1Completed = true;
                    changed = true;
                }

                // Stage1 완료 시 Stage2 해금(기존 규칙 유지)
                if (!data.stage2Unlocked)
                {
                    data.stage2Unlocked = true;
                    changed = true;
                }
            }
        }
        else if (stageId == 2)
        {
            // Stage2: 호감 달성한 종 기록
            added = stage2ClearedSet.Add(speciesId);

            // 원한다면 여기서 Stage3 해금 같은 로직도 넣을 수 있음.
            // 예: stage2ClearedSet.Count >= stageTotalSpeciesCount 이면 stage3Unlocked = true;
            // 지금은 "클리어한 종을 다시 나오지 않게"만 목표라서 생략.
        }

        if (added || changed)
            Save();
    }

    public bool IsStage1Completed()
    {
        // 구버전 호환: stage2Unlocked가 true면 stage1Completed도 true로 취급
        return data != null && (data.stage1Completed || data.stage2Unlocked);
    }

    public bool IsStage2Unlocked()
    {
        return data != null && data.stage2Unlocked;
    }

    public bool IsStage3Unlocked()
    {
        return data != null && data.stage3Unlocked;
    }

    public bool IsStage1SpeciesCleared(string speciesId)
    {
        if (string.IsNullOrWhiteSpace(speciesId)) return false;
        return stage1ClearedSet.Contains(speciesId);
    }

    public bool IsStage2SpeciesCleared(string speciesId)
    {
        if (string.IsNullOrWhiteSpace(speciesId)) return false;
        return stage2ClearedSet.Contains(speciesId);
    }


    public string GetSaveFilePath()
    {
        return FilePath;
    }

    [ContextMenu("DEV/Log Save File Path")]
    public void LogSaveFilePath()
    {
        Debug.Log("[SaveManager] Save file path: " + FilePath);
    }

    [ContextMenu("DEV/Reset Save Data (Delete File)")]
    public void DevResetSaveData()
    {
#if UNITY_EDITOR
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            data = new SaveData();
            stage1ClearedSet.Clear();

            Save();

            Debug.Log("[SaveManager] Save reset complete. New file created: " + FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] Reset failed: " + e.Message);
        }
#else
        Debug.LogWarning("[SaveManager] DevResetSaveData is editor-only.");
#endif
    }
}
