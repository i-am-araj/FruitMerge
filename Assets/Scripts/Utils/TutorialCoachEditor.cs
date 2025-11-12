#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialCoach))]
public class TutorialCoachEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);
        if (GUILayout.Button("Layout Dims Now"))
        {
            var t = target as TutorialCoach;
            if (t != null)
            {
                Undo.RecordObject(t, "Layout Dims Now");
                t.LayoutDims();
                EditorUtility.SetDirty(t);
            }
        }

        if (GUILayout.Button("Show (Editor)"))
        {
            var t = target as TutorialCoach;
            if (t != null) t.ShowForSeconds(2.5f);
        }

        if (GUILayout.Button("Hide (Editor)"))
        {
            var t = target as TutorialCoach;
            if (t != null) t.Hide();
        }
    }
}
#endif
