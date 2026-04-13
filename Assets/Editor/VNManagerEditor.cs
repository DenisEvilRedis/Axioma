using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VNManager))]
public sealed class VNManagerEditor : Editor
{
    private VNEditorUtility.GraphValidationResult _lastValidationResult;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Validate Story"))
        {
            _lastValidationResult = VNEditorUtility.ValidateGraph(serializedObject);
            LogValidationResult(_lastValidationResult);
        }

        DrawValidationSummary(_lastValidationResult);
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawValidationSummary(VNEditorUtility.GraphValidationResult result)
    {
        if (result == null)
        {
            return;
        }

        if (result.IsValid && result.Warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("Validation passed with no issues.", MessageType.Info);
            return;
        }

        if (result.Errors.Count > 0)
        {
            EditorGUILayout.HelpBox($"Errors: {result.Errors.Count}", MessageType.Error);
            for (int i = 0; i < result.Errors.Count; i++)
            {
                EditorGUILayout.HelpBox(result.Errors[i], MessageType.Error);
            }
        }

        if (result.Warnings.Count > 0)
        {
            EditorGUILayout.HelpBox($"Warnings: {result.Warnings.Count}", MessageType.Warning);
            for (int i = 0; i < result.Warnings.Count; i++)
            {
                EditorGUILayout.HelpBox(result.Warnings[i], MessageType.Warning);
            }
        }
    }

    private void LogValidationResult(VNEditorUtility.GraphValidationResult result)
    {
        if (result == null)
        {
            return;
        }

        if (result.IsValid && result.Warnings.Count == 0)
        {
            Debug.Log("[VNEditor] Validation passed with no issues.", target);
            return;
        }

        for (int i = 0; i < result.Errors.Count; i++)
        {
            Debug.LogError($"[VNEditor] {result.Errors[i]}", target);
        }

        for (int i = 0; i < result.Warnings.Count; i++)
        {
            Debug.LogWarning($"[VNEditor] {result.Warnings[i]}", target);
        }
    }
}
