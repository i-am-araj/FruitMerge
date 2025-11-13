using UnityEngine;
using CrazyGames;     // Official CrazyGames SDK
public class DataManager : MonoBehaviour
{
    public static DataManager instance;
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        CrazySDK.Init(() =>
        {
            Debug.Log("CrazySDK Initialized");
        });
    }
    public int GetInt(string key, int defaultValue)
    {
        int result = 0;
#if UNITY_WEBGL
        if (CrazySDK.IsAvailable)
        {
            result = CrazySDK.Data.GetInt(key, defaultValue);
        }
        else
        {
            result = PlayerPrefs.GetInt(key, defaultValue);
        }
#else
        result = PlayerPrefs.GetInt(key, defaultValue);
#endif
        return result;
    }
    public void SetInt(string key, int value)
    {
#if UNITY_WEBGL
        if (CrazySDK.IsAvailable)
        {
            CrazySDK.Data.SetInt(key, value);
        }
        else
        {
            PlayerPrefs.SetInt(key, value);
        }
#else
        PlayerPrefs.SetInt(key,value);
#endif
    }
    public string GetString(string key, string defaultValue)
    {
        string result = "";
#if UNITY_WEBGL
        if (CrazySDK.IsAvailable)
        {
            result = CrazySDK.Data.GetString(key, defaultValue);
        }
        else
        {
            result = PlayerPrefs.GetString(key, defaultValue);
        }
#else
        result = PlayerPrefs.GetString(key,defaultValue);
#endif
        return result;
    }

    public void SetString(string key, string value = "")
    {
#if UNITY_WEBGL
        if (CrazySDK.IsAvailable)
        {
            CrazySDK.Data.SetString(key, value);
        }
        else
        {
            PlayerPrefs.SetString(key, value);
        }
#else
        PlayerPrefs.SetString(key,value);
#endif
    }
}
