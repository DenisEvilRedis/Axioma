using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VNManager.BeatData))]
public sealed class VNBeatDataDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;
    private const float BoxPadding = 4f;
    private const float ButtonWidth = 64f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect boxRect = new Rect(position.x, position.y, position.width, position.height);
        GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

        Rect contentRect = new Rect(
            position.x + BoxPadding,
            position.y + BoxPadding,
            position.width - BoxPadding * 2f,
            position.height - BoxPadding * 2f);

        float y = contentRect.y;
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("beatId"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("mode"));

        SerializedProperty modeProperty = property.FindPropertyRelative("mode");
        VNManager.BeatMode mode = (VNManager.BeatMode)modeProperty.enumValueIndex;

        switch (mode)
        {
            case VNManager.BeatMode.Dialogue:
                DrawSpeakerFields(ref y, contentRect, property);
                DrawProperty(ref y, contentRect, property.FindPropertyRelative("background"));
                DrawMusicFields(ref y, contentRect, property);
                DrawCheckedBeatLinkField(
                    ref y,
                    contentRect,
                    property.FindPropertyRelative("nextBeatId"),
                    property.FindPropertyRelative("nextBeatSoundCategoryId"),
                    "Next Beat");
                break;

            case VNManager.BeatMode.Choice:
                DrawSpeakerFields(ref y, contentRect, property);
                DrawProperty(ref y, contentRect, property.FindPropertyRelative("background"));
                DrawMusicFields(ref y, contentRect, property);
                DrawProperty(ref y, contentRect, property.FindPropertyRelative("choices"));
                break;

            case VNManager.BeatMode.Branch:
                DrawSpeakerFields(ref y, contentRect, property);
                DrawProperty(ref y, contentRect, property.FindPropertyRelative("background"));
                DrawMusicFields(ref y, contentRect, property);
                DrawProperty(ref y, contentRect, property.FindPropertyRelative("branchRules"));
                DrawCheckedBeatLinkField(
                    ref y,
                    contentRect,
                    property.FindPropertyRelative("defaultNextBeatId"),
                    property.FindPropertyRelative("defaultNextBeatSoundCategoryId"),
                    "Default Next");
                break;

            case VNManager.BeatMode.Ending:
                DrawProperty(ref y, contentRect, property.FindPropertyRelative("endingToUnlock"));
                DrawProperty(ref y, contentRect, property.FindPropertyRelative("onEnterEffects"));
                break;
        }

        if (mode != VNManager.BeatMode.Ending)
        {
            DrawProperty(ref y, contentRect, property.FindPropertyRelative("onEnterEffects"));
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = BoxPadding * 2f;
        height += GetPropertyBlockHeight(property.FindPropertyRelative("beatId"));
        height += GetPropertyBlockHeight(property.FindPropertyRelative("mode"));

        VNManager.BeatMode mode = (VNManager.BeatMode)property.FindPropertyRelative("mode").enumValueIndex;
        switch (mode)
        {
            case VNManager.BeatMode.Dialogue:
                height += GetPropertyBlockHeight(property.FindPropertyRelative("speaker"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("speakerPortrait"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("bodyText"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("background"));
                height += GetMusicFieldsHeight(property);
                height += GetCheckedLinkHeight(property.FindPropertyRelative("nextBeatSoundCategoryId"));
                break;

            case VNManager.BeatMode.Choice:
                height += GetPropertyBlockHeight(property.FindPropertyRelative("speaker"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("speakerPortrait"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("bodyText"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("background"));
                height += GetMusicFieldsHeight(property);
                height += GetPropertyBlockHeight(property.FindPropertyRelative("choices"));
                break;

            case VNManager.BeatMode.Branch:
                height += GetPropertyBlockHeight(property.FindPropertyRelative("speaker"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("speakerPortrait"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("bodyText"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("background"));
                height += GetMusicFieldsHeight(property);
                height += GetPropertyBlockHeight(property.FindPropertyRelative("branchRules"));
                height += GetCheckedLinkHeight(property.FindPropertyRelative("defaultNextBeatSoundCategoryId"));
                break;

            case VNManager.BeatMode.Ending:
                height += GetPropertyBlockHeight(property.FindPropertyRelative("endingToUnlock"));
                height += GetPropertyBlockHeight(property.FindPropertyRelative("onEnterEffects"));
                break;
        }

        if (mode != VNManager.BeatMode.Ending)
        {
            height += GetPropertyBlockHeight(property.FindPropertyRelative("onEnterEffects"));
        }

        return height;
    }

    private static void DrawSpeakerFields(ref float y, Rect contentRect, SerializedProperty property)
    {
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("speaker"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("speakerPortrait"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("bodyText"));
    }

    private static void DrawMusicFields(ref float y, Rect contentRect, SerializedProperty property)
    {
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("musicCueId"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("stopMusic"));
    }

    private static void DrawProperty(ref float y, Rect contentRect, SerializedProperty property)
    {
        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect rect = new Rect(contentRect.x, y, contentRect.width, height);
        EditorGUI.PropertyField(rect, property, true);
        y += height + VerticalSpacing;
    }

    private static void DrawCheckedBeatLinkField(
        ref float y,
        Rect contentRect,
        SerializedProperty property,
        SerializedProperty soundCategoryProperty,
        string label)
    {
        Rect rowRect = new Rect(contentRect.x, y, contentRect.width, EditorGUIUtility.singleLineHeight);
        Rect fieldRect = new Rect(rowRect.x, rowRect.y, rowRect.width - ButtonWidth - 4f, rowRect.height);
        Rect buttonRect = new Rect(fieldRect.xMax + 4f, rowRect.y, ButtonWidth, rowRect.height);

        EditorGUI.PropertyField(fieldRect, property, new GUIContent(label));
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(property.stringValue)))
        {
            if (GUI.Button(buttonRect, "Check"))
            {
                bool exists = VNEditorUtility.BeatExists(property.serializedObject, property.stringValue);
                string state = exists ? "valid" : "invalid";
                Debug.Log($"[VNEditor] Link '{property.stringValue}' is {state}.", property.serializedObject.targetObject);
            }
        }

        y += rowRect.height + VerticalSpacing;
        DrawProperty(ref y, contentRect, soundCategoryProperty);
    }

    private static float GetPropertyBlockHeight(SerializedProperty property)
    {
        return EditorGUI.GetPropertyHeight(property, true) + VerticalSpacing;
    }

    private static float GetCheckedLinkHeight(SerializedProperty soundCategoryProperty)
    {
        return EditorGUIUtility.singleLineHeight + VerticalSpacing + GetPropertyBlockHeight(soundCategoryProperty);
    }

    private static float GetMusicFieldsHeight(SerializedProperty property)
    {
        return GetPropertyBlockHeight(property.FindPropertyRelative("musicCueId"))
            + GetPropertyBlockHeight(property.FindPropertyRelative("stopMusic"));
    }
}

[CustomPropertyDrawer(typeof(VNManager.ChoiceData))]
public sealed class VNChoiceDataDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;
    private const float BoxPadding = 4f;
    private const float ButtonWidth = 64f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

        Rect contentRect = new Rect(
            position.x + BoxPadding,
            position.y + BoxPadding,
            position.width - BoxPadding * 2f,
            position.height - BoxPadding * 2f);

        float y = contentRect.y;
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("label"));
        DrawCheckedBeatLinkField(
            ref y,
            contentRect,
            property.FindPropertyRelative("nextBeatId"),
            property.FindPropertyRelative("nextBeatSoundCategoryId"),
            "Next Beat");
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("effects"));

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return BoxPadding * 2f
            + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("label"), true) + VerticalSpacing
            + EditorGUIUtility.singleLineHeight + VerticalSpacing
            + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("nextBeatSoundCategoryId"), true) + VerticalSpacing
            + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("effects"), true) + VerticalSpacing;
    }

    private static void DrawProperty(ref float y, Rect contentRect, SerializedProperty property)
    {
        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect rect = new Rect(contentRect.x, y, contentRect.width, height);
        EditorGUI.PropertyField(rect, property, true);
        y += height + VerticalSpacing;
    }

    private static void DrawCheckedBeatLinkField(
        ref float y,
        Rect contentRect,
        SerializedProperty property,
        SerializedProperty soundCategoryProperty,
        string label)
    {
        Rect rowRect = new Rect(contentRect.x, y, contentRect.width, EditorGUIUtility.singleLineHeight);
        Rect fieldRect = new Rect(rowRect.x, rowRect.y, rowRect.width - ButtonWidth - 4f, rowRect.height);
        Rect buttonRect = new Rect(fieldRect.xMax + 4f, rowRect.y, ButtonWidth, rowRect.height);

        EditorGUI.PropertyField(fieldRect, property, new GUIContent(label));
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(property.stringValue)))
        {
            if (GUI.Button(buttonRect, "Check"))
            {
                bool exists = VNEditorUtility.BeatExists(property.serializedObject, property.stringValue);
                string state = exists ? "valid" : "invalid";
                Debug.Log($"[VNEditor] Link '{property.stringValue}' is {state}.", property.serializedObject.targetObject);
            }
        }

        y += rowRect.height + VerticalSpacing;
        DrawProperty(ref y, contentRect, soundCategoryProperty);
    }
}

[CustomPropertyDrawer(typeof(VNManager.BranchRuleData))]
public sealed class VNBranchRuleDataDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;
    private const float BoxPadding = 4f;
    private const float ButtonWidth = 64f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

        Rect contentRect = new Rect(
            position.x + BoxPadding,
            position.y + BoxPadding,
            position.width - BoxPadding * 2f,
            position.height - BoxPadding * 2f);

        float y = contentRect.y;
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("requiredFlag"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("forbiddenFlag"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("requiredDominantPath"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("minLead"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("minEscape"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("minVanity"));
        DrawProperty(ref y, contentRect, property.FindPropertyRelative("minHonesty"));
        DrawCheckedBeatLinkField(
            ref y,
            contentRect,
            property.FindPropertyRelative("nextBeatId"),
            property.FindPropertyRelative("nextBeatSoundCategoryId"),
            "Next Beat");

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return BoxPadding * 2f
            + GetPropertyBlockHeight(property.FindPropertyRelative("requiredFlag"))
            + GetPropertyBlockHeight(property.FindPropertyRelative("forbiddenFlag"))
            + GetPropertyBlockHeight(property.FindPropertyRelative("requiredDominantPath"))
            + GetPropertyBlockHeight(property.FindPropertyRelative("minLead"))
            + GetPropertyBlockHeight(property.FindPropertyRelative("minEscape"))
            + GetPropertyBlockHeight(property.FindPropertyRelative("minVanity"))
            + GetPropertyBlockHeight(property.FindPropertyRelative("minHonesty"))
            + EditorGUIUtility.singleLineHeight + VerticalSpacing
            + GetPropertyBlockHeight(property.FindPropertyRelative("nextBeatSoundCategoryId"));
    }

    private static void DrawProperty(ref float y, Rect contentRect, SerializedProperty property)
    {
        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect rect = new Rect(contentRect.x, y, contentRect.width, height);
        EditorGUI.PropertyField(rect, property, true);
        y += height + VerticalSpacing;
    }

    private static void DrawCheckedBeatLinkField(
        ref float y,
        Rect contentRect,
        SerializedProperty property,
        SerializedProperty soundCategoryProperty,
        string label)
    {
        Rect rowRect = new Rect(contentRect.x, y, contentRect.width, EditorGUIUtility.singleLineHeight);
        Rect fieldRect = new Rect(rowRect.x, rowRect.y, rowRect.width - ButtonWidth - 4f, rowRect.height);
        Rect buttonRect = new Rect(fieldRect.xMax + 4f, rowRect.y, ButtonWidth, rowRect.height);

        EditorGUI.PropertyField(fieldRect, property, new GUIContent(label));
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(property.stringValue)))
        {
            if (GUI.Button(buttonRect, "Check"))
            {
                bool exists = VNEditorUtility.BeatExists(property.serializedObject, property.stringValue);
                string state = exists ? "valid" : "invalid";
                Debug.Log($"[VNEditor] Link '{property.stringValue}' is {state}.", property.serializedObject.targetObject);
            }
        }

        y += rowRect.height + VerticalSpacing;
        DrawProperty(ref y, contentRect, soundCategoryProperty);
    }

    private static float GetPropertyBlockHeight(SerializedProperty property)
    {
        return EditorGUI.GetPropertyHeight(property, true) + VerticalSpacing;
    }
}
