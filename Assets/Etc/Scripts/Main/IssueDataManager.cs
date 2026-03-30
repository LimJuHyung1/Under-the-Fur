using UnityEngine;
using System.Collections.Generic;

public class IssueDataManager : MonoBehaviour
{
    public static IssueDataManager Instance;

    public struct IssueData
    {
        public string id;
        public string displayName; // 추가됨
        public string issueTitle;
        public string description;
    }

    private Dictionary<string, IssueData> issueTable = new Dictionary<string, IssueData>();

    void Awake()
    {
        Instance = this;
        LoadCSV();
    }

    void LoadCSV()
    {
        // Assets/Resources/ 내부의 'Main' 폴더 안에 있는 'Issues_Data' 파일을 로드합니다.
        // 확장자 .csv는 생략해야 합니다.
        TextAsset csvFile = Resources.Load<TextAsset>("Main/Issues_Data");

        if (csvFile == null)
        {
            Debug.LogError("CSV 파일을 찾을 수 없습니다! 경로를 확인하세요: Assets/Resources/Main/Issues_Data.csv");
            return;
        }

        string[] lines = csvFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] row = ParseCSVRow(lines[i]);

            // 열 순서: 0:ID, 1:DisplayName, 2:Issue_Title, 3:Description
            if (row.Length >= 4)
            {
                IssueData data = new IssueData
                {
                    id = row[0].Trim(),
                    displayName = row[1].Trim(),
                    issueTitle = row[2].Trim(),
                    description = row[3].Trim()
                };
                issueTable[data.id] = data;
            }
        }
    }
    // 쉼표와 큰따옴표가 포함된 설명을 안전하게 나누기 위한 로직
    string[] ParseCSVRow(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        foreach (char c in line)
        {
            if (c == '\"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else currentField += c;
        }
        result.Add(currentField);
        return result.ToArray();
    }

    public IssueData GetIssue(string id) => issueTable.ContainsKey(id) ? issueTable[id] : default;
}