using UnityEngine;

public class MainSceneBackground : MonoBehaviour
{
    [Header("Resources 폴더 경로")]
    [SerializeField] private string backgroundResourcesPath = "Main/Background";

    [Header("배경 프리팹 내부의 스폰 루트 오브젝트 이름")]
    [SerializeField] private string spawnPointsChildName = "SpawnPoints";

    [Header("시작 시 자동 로드")]
    [SerializeField] private bool loadOnAwake = true;

    private GameObject currentBackgroundInstance;
    private Transform spawnPointsRoot;
    private Transform[] spawnPoints = new Transform[0];

    public Transform SpawnPointsRoot => spawnPointsRoot;
    public Transform[] SpawnPoints => spawnPoints;

    private void Awake()
    {
        if (loadOnAwake)
            LoadRandomBackground();
    }

    public void LoadRandomBackground()
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>(backgroundResourcesPath);
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("[SessionBackground] 배경 프리팹을 찾지 못했습니다. Resources/" + backgroundResourcesPath + " 경로를 확인하세요.");
            return;
        }

        int index = Random.Range(0, prefabs.Length);
        LoadBackground(prefabs[index]);
    }

    public void LoadBackground(GameObject backgroundPrefab)
    {
        if (backgroundPrefab == null)
        {
            Debug.LogError("[SessionBackground] backgroundPrefab 이 null 입니다.");
            return;
        }

        ClearCurrentBackground();

        currentBackgroundInstance = Instantiate(backgroundPrefab, transform);
        currentBackgroundInstance.name = backgroundPrefab.name;

        CacheSpawnPoints();
    }

    public void ClearCurrentBackground()
    {
        if (currentBackgroundInstance == null)
            return;

        if (Application.isPlaying)
            Destroy(currentBackgroundInstance);
        else
            DestroyImmediate(currentBackgroundInstance);

        currentBackgroundInstance = null;
        spawnPointsRoot = null;
        spawnPoints = new Transform[0];
    }

    private void CacheSpawnPoints()
    {
        spawnPointsRoot = null;
        spawnPoints = new Transform[0];

        if (currentBackgroundInstance == null)
            return;

        Transform bgRoot = currentBackgroundInstance.transform;

        // 1) 기본 이름으로 찾기
        spawnPointsRoot = bgRoot.Find(spawnPointsChildName);

        // 2) 못 찾으면 하위 전체에서 이름에 "spawn" 포함된 오브젝트로 fallback
        if (spawnPointsRoot == null)
            spawnPointsRoot = FindChildContains(bgRoot, "spawn");

        if (spawnPointsRoot == null)
        {
            Debug.LogWarning("[SessionBackground] SpawnPoints를 찾지 못했습니다. 배경 프리팹 안에 '" + spawnPointsChildName + "' 자식 오브젝트를 만들어 주세요.");
            return;
        }

        int count = spawnPointsRoot.childCount;
        spawnPoints = new Transform[count];
        for (int i = 0; i < count; i++)
            spawnPoints[i] = spawnPointsRoot.GetChild(i);
    }

    private Transform FindChildContains(Transform root, string tokenLower)
    {
        tokenLower = tokenLower.ToLowerInvariant();

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == root) continue;

            string n = t.name;
            if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains(tokenLower))
                return t;
        }

        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Load Random Background")]
    private void DebugLoadRandomBackground()
    {
        LoadRandomBackground();
    }
#endif
}
