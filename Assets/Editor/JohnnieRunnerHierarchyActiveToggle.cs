using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JohnnieRunnerHierarchyActiveToggle
///
/// Two things are drawn on every Hierarchy row:
///   1. Tree guide in the indent gutter (vertical rails + elbow), so the
///      parent/child structure reads without expanding anything.
///   2. Eye icon on the right edge: open eye = active, crossed-out eye =
///      inactive. Click it to toggle that GameObject only (with Undo).
///
/// Editor-only. Unity 2020.3 has no built-in Hierarchy visibility toggle,
/// and hierarchyWindowItemOnGUI gives us no depth / expanded state, so depth
/// is derived from transform.parent and cached.
/// </summary>
[InitializeOnLoad]
internal static class JohnnieRunnerHierarchyActiveToggle
{
    // ---- eye toggle ----
    const float k_IconSize = 16f;
    const float k_RightPad = 2f;
    const string k_EyeOnPath = "Assets/Editor/Icons/EyeOn.png";
    const string k_EyeOffPath = "Assets/Editor/Icons/EyeOff.png";

    // ---- tree guide metrics ----
    // Unity 2020.3 renders one hierarchy indent step as 14px, and the depth-0
    // foldout arrow sits at ~14px. If a future Unity version changes the
    // indent, tweak k_Indent / k_BaseIndent and the guides line up again.
    const float k_Indent = 14f;
    const float k_BaseIndent = 14f;
    const float k_LineWidth = 1f;
    const float k_StubLength = 7f;

    static Texture2D s_EyeOn;
    static Texture2D s_EyeOff;

    // instanceID -> depth, cleared whenever the hierarchy changes.
    static readonly Dictionary<int, int> s_DepthCache = new Dictionary<int, int>();

    static JohnnieRunnerHierarchyActiveToggle()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyRow;
        EditorApplication.hierarchyChanged += s_DepthCache.Clear;
    }

    static void OnHierarchyRow(int instanceID, Rect row)
    {
        var target = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (target == null)
            return;

        DrawTreeGuide(target, row);
        DrawEyeToggle(target, row);
    }

    // ---------------------------------------------------------------- tree

    static void DrawTreeGuide(GameObject target, Rect row)
    {
        // Drawing-only work: never do it during Layout / input events.
        if (Event.current.type != EventType.Repaint)
            return;

        var t = target.transform;
        if (t.parent == null)
            return; // scene root has nothing to connect to

        int depth = GetDepth(t);
        if (depth <= 0)
            return;

        var color = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.13f)
            : new Color(0f, 0f, 0f, 0.15f);

        // Vertical rails for every ancestor level. Because Unity only calls us
        // for rows it actually draws, a full-height rail is automatically
        // correct even when the branch above is collapsed.
        for (int level = 0; level < depth - 1; level++)
        {
            float railX = row.x + k_BaseIndent + level * k_Indent + k_Indent * 0.5f;
            EditorGUI.DrawRect(new Rect(railX, row.y, k_LineWidth, row.height), color);
        }

        // The elbow joining this row to its parent.
        float cx = row.x + k_BaseIndent + (depth - 1) * k_Indent + k_Indent * 0.5f;
        float elbowY = row.y + row.height * 0.5f;

        // Upper arm is always drawn; the lower arm only when a sibling follows,
        // which is what turns a plain rail into a "last child" corner.
        EditorGUI.DrawRect(new Rect(cx, row.y, k_LineWidth, elbowY - row.y), color);

        bool isLastSibling = t.GetSiblingIndex() == t.parent.childCount - 1;
        if (!isLastSibling)
            EditorGUI.DrawRect(new Rect(cx, elbowY, k_LineWidth, row.yMax - elbowY), color);

        // Horizontal stub pointing at the child's own foldout gutter.
        EditorGUI.DrawRect(new Rect(cx, elbowY, k_StubLength, k_LineWidth), color);
    }

    static int GetDepth(Transform t)
    {
        int id = t.GetInstanceID();
        int depth;
        if (s_DepthCache.TryGetValue(id, out depth))
            return depth;

        depth = 0;
        for (var p = t.parent; p != null; p = p.parent)
            depth++;

        s_DepthCache[id] = depth;
        return depth;
    }

    // ----------------------------------------------------------------- eye

    static void DrawEyeToggle(GameObject target, Rect row)
    {
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
        if (Event.current.type != EventType.Repaint)
            return;

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
