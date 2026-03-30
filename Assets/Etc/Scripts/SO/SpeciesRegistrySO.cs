using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpeciesRegistry", menuName = "Under the Fur/Species Registry")]
public class SpeciesRegistrySO : ScriptableObject
{
    // 모든 동물 SO를 담는 리스트
    public List<AnimalSpeciesSO> allSpecies = new List<AnimalSpeciesSO>();

    // ID로 동물을 찾는 헬퍼 메서드
    public AnimalSpeciesSO GetSpeciesById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return allSpecies.Find(s => s != null && s.id == id);
    }

    // [선택 사항] 스테이지별로 동물을 필터링하고 싶을 때 사용
    public List<AnimalSpeciesSO> GetSpeciesByStage(int stageId)
    {
        return allSpecies.FindAll(s => s != null && (int)s.stage2SetId == stageId); // stage2SetId 등 기존 필드 활용
    }
}