using UnityEditor;
using UnityEngine;

/// <summary>
/// JohnnieRunnerHierarchyActiveToggle
/// Draws an eye icon on the right edge of every Hierarchy row:
/// open eye = active, crossed-out eye = inactive.
/// Click toggles that GameObject only (with Undo). Editor-only.
/// </summary>
[InitializeOnLoad]
internal static class JohnnieRunnerHierarchyActiveToggle
{
    const float k_IconSize = 16f;
    const float k_RightPad = 2f;
    const string k_EyeOnPath = "Assets/Editor/Icons/EyeOn.png";
    const string k_EyeOffPath = "Assets/Editor/Icons/EyeOff.png";

    static Texture2D s_EyeOn;
    static Texture2D s_EyeOff;

    static JohnnieRunnerHierarchyActiveToggle()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyRow;
    }

    static void OnHierarchyRow(int instanceID, Rect row)
    {
        var target = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (target == null)
            return;

        float size = Mathf.Min(k_IconSize, row.height - 2f);
        var box = new Rect(row.xMax - size - k_RightPad, row.y + (row.height - size) * 0.5f, size, size);

        var current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && box.Contains(current.mousePosition))
        {
            // Consume the click so the row neither selects nor starts a drag.
            current.Use();
            SetActive(target, !target.activeSelf);
        }

        DrawIcon(box, target.activeSelf);
    }

    static void DrawIcon(Rect box, bool active)
    {
        if (s_EyeOn == null || s_EyeOff == null)
        {
            s_EyeOn = AssetDatabase.LoadAssetAtPath<Texture2D>(k_EyeOnPath);
            s_EyeOff = AssetDatabase.LoadAssetAtPath<Texture2D>(k_EyeOffPath);
        }

        var icon = active ? s_EyeOn : s_EyeOff;
        if (icon == null)
            return;

        // Icons are solid white; tint so they read on both editor skins.
        var previous = GUI.color;
        GUI.color = EditorGUIUtility.isProSkin
            ? new Color(0.78f, 0.78f, 0.78f)
            : new Color(0.30f, 0.30f, 0.30f);
        GUI.DrawTexture(box, icon, ScaleMode.ScaleToFit, true);
        GUI.color = previous;
    }

    static void SetActive(GameObject target, bool value)
    {
        // Deferred so we never mutate the hierarchy while Unity is iterating rows to draw them.
        EditorApplication.delayCall += () =>
        {
            if (target == null)
                return;

            Undo.RecordObject(target, value ? "Activate" : "Deactivate");
            target.SetActive(value);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);

            EditorApplication.RepaintHierarchyWindow();
        };
    }
}
