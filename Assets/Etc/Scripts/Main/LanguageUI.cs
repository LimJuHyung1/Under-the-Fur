using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections;

public class LanguageUI : MonoBehaviour
{
    public AudioSource music;
    // 언어 코드를 저장할 키 이름입니다.
    private const string LanguageCodeKey = "SavedLanguageCode";

    private IEnumerator Start()
    {
        // 1. 로컬라이제이션 시스템이 초기화될 때까지 기다립니다.
        yield return LocalizationSettings.InitializationOperation;

        // 2. 저장된 언어 코드가 있는지 확인합니다.
        string savedCode = PlayerPrefs.GetString(LanguageCodeKey, "");

        if (!string.IsNullOrEmpty(savedCode))
        {
            // 저장된 코드가 있다면 해당 언어로 설정을 변경합니다.
            Locale identifier = LocalizationSettings.AvailableLocales.GetLocale(savedCode);
            if (identifier != null)
            {
                LocalizationSettings.SelectedLocale = identifier;
            }

            // 이미 언어를 설정했으므로 UI를 끄고 음악을 재생합니다.
            HideUIAndPlayMusic();
        }
    }

    // 인덱스로 언어 변경 (버튼 연결용)
    public void ChangeLanguage(int localeIndex)
    {
        StartCoroutine(SetLocale(localeIndex));
    }

    // 코드로 언어 변경 (버튼 연결용)
    public void ChangeLanguageByCode(string localeCode)
    {
        Locale identifier = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
        if (identifier != null)
        {
            LocalizationSettings.SelectedLocale = identifier;
            // 선택한 언어 코드를 저장합니다.
            SaveLanguageCode(localeCode);
        }

        HideUIAndPlayMusic();
    }

    private IEnumerator SetLocale(int localeIndex)
    {
        yield return LocalizationSettings.InitializationOperation;
        Locale selectedLocale = LocalizationSettings.AvailableLocales.Locales[localeIndex];
        LocalizationSettings.SelectedLocale = selectedLocale;

        // 선택한 언어의 코드를 저장합니다. (예: "ko", "en")
        SaveLanguageCode(selectedLocale.Identifier.Code);
        HideUIAndPlayMusic();
    }

    // 언어 코드를 기기에 저장하는 함수
    private void SaveLanguageCode(string code)
    {
        PlayerPrefs.SetString(LanguageCodeKey, code);
        PlayerPrefs.Save();
    }

    // UI 비활성화 및 음악 재생 공통 로직
    private void HideUIAndPlayMusic()
    {
        gameObject.SetActive(false);
        if (music != null && !music.isPlaying)
        {
            music.Play();
        }
    }
}