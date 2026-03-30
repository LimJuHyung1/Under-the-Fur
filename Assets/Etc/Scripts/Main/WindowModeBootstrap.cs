using UnityEngine;

public class WindowModeBootstrap : MonoBehaviour
{
    [Header("Square Window Size")]
    [SerializeField] private int size = 900; // 900x900, 1080x1080 등

    private void Awake()
    {
        Apply();
    }

    private void Apply()
    {
        // Windowed + Square
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(size, size, FullScreenMode.Windowed);

        // (선택) 특정 해상도에서만 고정하고 싶으면 리사이즈 허용 옵션은
        // PlayerSettings 쪽에서 제어하는 편이 더 안정적이야.
    }
}
