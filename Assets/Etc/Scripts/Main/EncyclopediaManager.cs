using System.Collections.Generic;
using UnityEngine;

public class EncyclopediaManager : MonoBehaviour
{
    public List<GameObject> group1 = new List<GameObject>();
    public List<GameObject> group2 = new List<GameObject>();
    public List<GameObject> group3 = new List<GameObject>();
    public List<GameObject> group4 = new List<GameObject>();

    [Header("Rule")]
    [SerializeField] private int minClearedToReveal = 4;

    [Header("Colors")]
    [SerializeField] private Color lockedColor = Color.black;
    [SerializeField] private Color unlockedColor = Color.white;

    public void RefreshAll()
    {
        var save = SaveManager.Instance;
        if (save == null || save.Data == null) return;

        int clearedCount = (save.Data.stage2ClearedSpeciesIds != null) ? save.Data.stage2ClearedSpeciesIds.Count : 0;
        bool allowReveal = clearedCount >= minClearedToReveal;

        ApplyGroup(group1, allowReveal);
        ApplyGroup(group2, allowReveal);
        ApplyGroup(group3, allowReveal);
        ApplyGroup(group4, allowReveal);
    }

    private void ApplyGroup(List<GameObject> group, bool allowReveal)
    {
        if (group == null) return;

        for (int i = 0; i < group.Count; i++)
        {
            var go = group[i];
            if (go == null) continue;

            var sr = go.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) continue;

            if (!allowReveal)
            {
                sr.color = lockedColor;
                continue;
            }

            string id = Normalize(go.name);
            bool cleared = IsStage2Cleared(id);
            sr.color = cleared ? unlockedColor : lockedColor;
        }
    }

    // EncyclopediaManager.cs 에 추가

    /// <summary>
    /// 특정 동물이 도감에서 활성화(잠금 해제)되었는지 확인합니다.
    /// </summary>
    public bool IsUnlocked(string speciesId)
    {
        var save = SaveManager.Instance;
        if (save == null || save.Data == null) return false;

        // 1. 전체 잠금 해제 조건 확인 (최소 minClearedToReveal마리 이상 클리어했는지)
        int clearedCount = (save.Data.stage2ClearedSpeciesIds != null) ? save.Data.stage2ClearedSpeciesIds.Count : 0;
        bool allowReveal = clearedCount >= minClearedToReveal;

        if (!allowReveal) return false;

        // 2. 해당 동물이 실제 클리어 리스트에 있는지 확인
        return IsStage2Cleared(speciesId);
    }

    private bool IsStage2Cleared(string speciesId)
    {
        var save = SaveManager.Instance;
        if (save == null || save.Data == null || save.Data.stage2ClearedSpeciesIds == null) return false;

        string needle = Normalize(speciesId);
        var list = save.Data.stage2ClearedSpeciesIds;

        for (int i = 0; i < list.Count; i++)
        {
            if (Normalize(list[i]) == needle)
                return true;
        }

        return false;
    }

    private string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Trim().ToLowerInvariant();
    }
}
