using SmallScaleInc.TopDownPixelCharactersPack1;
using System.Collections.Generic;
using UnityEngine;

public class MainSceneEnvironmentDirector : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private MainSceneBackground background;

    [Header("Resources 경로")]
    [SerializeField] private string stage2SpeciesPath = "MyAnimals/SpeciesSO/Stage2";
    [SerializeField] private string backgroundsPrefabPath = "Main/Background";

    [Header("동물 베이스 프리팹 (1개)")]
    [SerializeField] private string baseAnimalPrefabPath = "Main/Animals"; // 폴더(LoadAll)로 사용
    [SerializeField] private string baseAnimalExactPath = ""; // 정확 경로로 하나 지정하고 싶으면 "Main/Animals/AnimalBase" 같은 값

    [Header("스폰 설정")]
    [SerializeField] private int spawnCount = 7;
    [SerializeField] private bool allowDuplicate = false;

    [Header("디버그")]
    [SerializeField] private bool verboseLog = true;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Start()
    {
        if (background == null)
            background = FindFirstObjectByType<MainSceneBackground>();

        Run();
    }

    public void Run()
    {
        ClearSpawned();

        if (background == null)
        {
            Debug.LogError("[MainSceneEnvironmentDirector] MainSceneBackground를 찾지 못했습니다.");
            return;
        }

        // 1) Stage2 SO 로드
        AnimalSpeciesSO[] speciesList = Resources.LoadAll<AnimalSpeciesSO>(stage2SpeciesPath);
        if (speciesList == null || speciesList.Length == 0)
        {
            Debug.LogError("[MainSceneEnvironmentDirector] SpeciesSO를 찾지 못했습니다. Resources/" + stage2SpeciesPath);
            return;
        }

        // 2) 배경 프리팹 로드
        GameObject[] bgPrefabs = Resources.LoadAll<GameObject>(backgroundsPrefabPath);
        if (bgPrefabs == null || bgPrefabs.Length == 0)
        {
            Debug.LogError("[MainSceneEnvironmentDirector] 배경 프리팹을 찾지 못했습니다. Resources/" + backgroundsPrefabPath);
            return;
        }

        Dictionary<string, GameObject> bgByEnv = BuildBackgroundMap(bgPrefabs);

        // 3) 환경별 후보군 구성 (배경 존재 + AnimatorController 존재)
        Dictionary<string, List<AnimalSpeciesSO>> candidatesByEnv = new Dictionary<string, List<AnimalSpeciesSO>>();

        int rejectEnvEmpty = 0;
        int rejectBgMissing = 0;
        int rejectControllerMissing = 0;

        for (int i = 0; i < speciesList.Length; i++)
        {
            AnimalSpeciesSO so = speciesList[i];
            if (so == null) continue;

            string env = NormalizeEnvKey(GetEnvironmentKeyFromSO(so));
            if (string.IsNullOrEmpty(env))
            {
                rejectEnvEmpty++;
                continue;
            }

            if (!bgByEnv.ContainsKey(env))
            {
                rejectBgMissing++;
                continue;
            }

            if (so.AnimatorController == null)
            {
                rejectControllerMissing++;
                continue;
            }

            if (!candidatesByEnv.TryGetValue(env, out List<AnimalSpeciesSO> list))
            {
                list = new List<AnimalSpeciesSO>();
                candidatesByEnv.Add(env, list);
            }
            list.Add(so);
        }

        if (verboseLog)
        {
            Debug.Log("[MainSceneEnvironmentDirector] candidates env count=" + candidatesByEnv.Count
                + " rejectEnvEmpty=" + rejectEnvEmpty
                + " rejectBgMissing=" + rejectBgMissing
                + " rejectControllerMissing=" + rejectControllerMissing);
        }

        if (candidatesByEnv.Count == 0)
        {
            Debug.LogError("[MainSceneEnvironmentDirector] 환경 매칭 후보가 없습니다. (SO env / 배경 이름 / AnimatorController 누락을 확인하세요)");
            return;
        }

        // 4) env 하나 선택
        string selectedEnv = PickRandomKey(candidatesByEnv);
        GameObject bgPrefabSelected = bgByEnv[selectedEnv];

        // 5) 배경 로드
        background.LoadBackground(bgPrefabSelected);

        // 6) 스폰 포인트 확인
        Transform[] spawnPoints = background.SpawnPoints;
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[MainSceneEnvironmentDirector] SpawnPoints가 비어있습니다. 배경 프리팹 내부에 SpawnPoints와 자식 포인트들을 만들어 주세요.");
            return;
        }

        // 7) 베이스 동물 프리팹 1개 로드
        GameObject baseAnimalPrefab = LoadBaseAnimalPrefab();
        if (baseAnimalPrefab == null)
        {
            Debug.LogError("[MainSceneEnvironmentDirector] 베이스 동물 프리팹을 찾지 못했습니다. baseAnimalExactPath 또는 Resources/" + baseAnimalPrefabPath + " 폴더를 확인하세요.");
            return;
        }

        if (verboseLog)
            Debug.Log("[MainSceneEnvironmentDirector] base animal prefab = " + baseAnimalPrefab.name);

        // 8) 선택 env 후보로 동물 스폰
        List<AnimalSpeciesSO> pool = candidatesByEnv[selectedEnv];

        int count = Mathf.Min(spawnCount, spawnPoints.Length);
        if (!allowDuplicate)
            count = Mathf.Min(count, pool.Count);

        if (count <= 0)
        {
            Debug.LogError("[MainSceneEnvironmentDirector] 스폰할 동물이 없습니다. env=" + selectedEnv);
            return;
        }

        List<int> idx = new List<int>(pool.Count);
        for (int i = 0; i < pool.Count; i++) idx.Add(i);
        Shuffle(idx);

        Transform parent = CreateOrFindAnimalsRoot(background.transform);

        for (int i = 0; i < count; i++)
        {
            AnimalSpeciesSO so = allowDuplicate ? pool[Random.Range(0, pool.Count)] : pool[idx[i]];
            if (so == null) continue;

            Transform sp = spawnPoints[i];

            // 베이스 프리팹 복제
            GameObject inst = Instantiate(baseAnimalPrefab, sp.position, sp.rotation, parent);
            inst.name = "Main_" + so.id;

            // AnimatorController 주입 (외형 변경 핵심)
            Animator anim = inst.GetComponentInChildren<Animator>(true);
            if (anim != null)
                anim.runtimeAnimatorController = so.AnimatorController;

            // Wander는 controller 주입 후 붙이는게 안전
            if (inst.GetComponent<MainSceneAnimalWander>() == null)
                inst.AddComponent<MainSceneAnimalWander>();

            spawned.Add(inst);
        }

        Debug.Log("[MainSceneEnvironmentDirector] selectedEnv=" + selectedEnv + " spawned=" + spawned.Count);
    }

    public void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }
        spawned.Clear();
    }

    private GameObject LoadBaseAnimalPrefab()
    {
        if (!string.IsNullOrEmpty(baseAnimalExactPath))
        {
            GameObject p = Resources.Load<GameObject>(baseAnimalExactPath);
            if (p != null) return p;
        }

        GameObject[] prefabs = Resources.LoadAll<GameObject>(baseAnimalPrefabPath);
        if (prefabs == null || prefabs.Length == 0)
            return null;

        // 폴더에 1개만 있는 구조면 첫 번째가 곧 베이스 프리팹
        return prefabs[0];
    }

    // SO likes의 Environment key 우선
    private string GetEnvironmentKeyFromSO(AnimalSpeciesSO so)
    {
        if (so == null) return string.Empty;

        if (so.likes != null)
        {
            for (int i = 0; i < so.likes.Count; i++)
            {
                var item = so.likes[i];
                if (item == null) continue;

                if (item.category == PreferenceCategory.Environment && !string.IsNullOrEmpty(item.key))
                    return item.key;
            }
        }

        return so.EnvironmentKey;
    }

    private Dictionary<string, GameObject> BuildBackgroundMap(GameObject[] prefabs)
    {
        string[] known = new string[]
        {
            "forestwithsnow",
            "northpole",
            "highlands",
            "savanna",
            "desert",
            "forest",
            "farm",
            "jungle",
            "swamp",
            "valley",
            "city"
        };

        Dictionary<string, GameObject> map = new Dictionary<string, GameObject>();

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject p = prefabs[i];
            if (p == null) continue;

            string pn = NormalizeEnvKey(p.name);

            for (int k = 0; k < known.Length; k++)
            {
                if (pn == known[k] || pn.Contains(known[k]))
                {
                    if (!map.ContainsKey(known[k]))
                        map.Add(known[k], p);
                }
            }
        }

        return map;
    }

    private string NormalizeEnvKey(string s)
    {
        s = NormalizeForMatch(s);

        // 흔한 오타 흡수
        if (s == "savana") s = "savanna";

        return s;
    }

    private string NormalizeForMatch(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        s = s.Trim().ToLowerInvariant();
        s = s.Replace(" ", "");
        s = s.Replace("_", "");
        s = s.Replace("-", "");
        return s;
    }

    private string PickRandomKey(Dictionary<string, List<AnimalSpeciesSO>> dict)
    {
        int r = Random.Range(0, dict.Count);
        int i = 0;
        foreach (var kv in dict)
        {
            if (i == r) return kv.Key;
            i++;
        }
        foreach (var kv in dict) return kv.Key;
        return string.Empty;
    }

    private Transform CreateOrFindAnimalsRoot(Transform root)
    {
        if (root == null) root = transform;

        Transform existing = root.Find("MainAnimals");
        if (existing != null) return existing;

        GameObject go = new GameObject("MainAnimals");
        go.transform.SetParent(root, false);
        return go.transform;
    }

    private void Shuffle(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            int tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }
}
