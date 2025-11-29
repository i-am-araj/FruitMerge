using UnityEngine;
public class DataManager : MonoBehaviour
{
    public static DataManager instance;
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }
    public int GetInt(string key, int defaultValue)
    {
        int result = 0;
#if UNITY_WEBGL
        result = PlayerPrefs.GetInt(key, defaultValue);

#else
        result = PlayerPrefs.GetInt(key, defaultValue);
#endif
        return result;
    }
    public void SetInt(string key, int value)
    {
#if UNITY_WEBGL
        PlayerPrefs.SetInt(key, value);
#else
        PlayerPrefs.SetInt(key,value);
#endif
    }
    public string GetString(string key, string defaultValue)
    {
        string result = "";
#if UNITY_WEBGL
        result = PlayerPrefs.GetString(key, defaultValue);

#else
        result = PlayerPrefs.GetString(key,defaultValue);
#endif
        return result;
    }

    public void SetString(string key, string value = "")
    {
#if UNITY_WEBGL
        PlayerPrefs.SetString(key, value);

#else
        PlayerPrefs.SetString(key,value);
#endif
    }
}
