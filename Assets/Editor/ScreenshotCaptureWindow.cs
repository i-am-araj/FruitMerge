// Assets/Editor/ScreenshotCaptureWindow.cs
using UnityEngine;
using UnityEditor;
using System.IO;

public class ScreenshotCaptureWindow : EditorWindow
{
    // Resolution
    private int width = 1080;
    private int height = 1920;

    // Camera to render from
    private Camera captureCamera;

    // File settings
    private string folderName = "Screenshots";
    private string filePrefix = "screenshot_";

    [MenuItem("Tools/Screenshot Capture")]
    public static void OpenWindow()
    {
        GetWindow<ScreenshotCaptureWindow>("Screenshot Capture");
    }

    private void OnGUI()
    {
        GUILayout.Label("Screenshot Settings", EditorStyles.boldLabel);

        // Resolution
        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);

        EditorGUILayout.Space();

        // Camera
        captureCamera = (Camera)EditorGUILayout.ObjectField("Capture Camera", captureCamera, typeof(Camera), true);
        if (captureCamera == null)
        {
            EditorGUILayout.HelpBox("If left empty, will use Camera.main at capture time.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // Output folder & prefix
        folderName = EditorGUILayout.TextField("Folder (under Assets)", folderName);
        filePrefix = EditorGUILayout.TextField("File Prefix", filePrefix);

        EditorGUILayout.Space();

        if (GUILayout.Button("Capture Screenshot", GUILayout.Height(40)))
        {
            CaptureScreenshot();
        }
    }

    private void CaptureScreenshot()
    {
        // Decide which camera to use
        Camera cam = captureCamera != null ? captureCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError("[ScreenshotCapture] No camera found. Assign a camera or ensure Camera.main exists.");
            return;
        }

        // Prepare paths
        string folderPath = Path.Combine(Application.dataPath, folderName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{filePrefix}{width}x{height}_{timestamp}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        // Create RenderTexture & Texture2D
        RenderTexture rt = new RenderTexture(width, height, 24);
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        // Backup current camera settings
        RenderTexture prevRT = cam.targetTexture;
        float prevAspect = cam.aspect;

        try
        {
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;

            // Render the camera
            cam.Render();

            // Read pixels
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Encode to PNG
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);

            Debug.Log($"[ScreenshotCapture] Saved screenshot: {fullPath}");
        }
        finally
        {
            // Restore state
            cam.targetTexture = prevRT;
            cam.aspect = prevAspect;
            RenderTexture.active = null;

            // Cleanup
            if (rt != null)
            {
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            if (tex != null)
            {
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.Refresh();
        }
    }
}
