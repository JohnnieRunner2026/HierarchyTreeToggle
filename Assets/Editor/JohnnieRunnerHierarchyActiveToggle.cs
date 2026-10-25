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
    const float k_ScrollbarWidth = 14f;
    const string k_EyeOnPath = "Assets/Editor/Icons/EyeOn.png";
    const string k_EyeOffPath = "Assets/Editor/Icons/EyeOff.png";

    // Both PNGs are 32x32 with the glyph occupying only a small middle band.
    // UVs point at that band so the eye is not shrunk into a few pixels.
    static readonly Rect k_EyeOnUv = new Rect(8f / 32f, 12f / 32f, 16f / 32f, 9f / 32f);
    static readonly Rect k_EyeOffUv = new Rect(7f / 32f, 11f / 32f, 18f / 32f, 10f / 32f);

    // ---- tree guide metrics ----
    // Unity 2020.3 indents one hierarchy step by 14px. The guide is positioned
    // relative to the row rect Unity hands us, so only this step ever needs
    // tuning if a future version changes the indent.
    const float k_Indent = 14f;
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
            return; // scene-name pseudo row: nothing to draw against

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
        int depth = GetDepth(t);

        // Unity already indents every row (14px per level) and it also shows
        // scene roots one level in, under the "scene name" pseudo row. So row.x
        // encodes the visual column already — derive the gutter from it instead
        // of adding offsets, otherwise the guide lands on top of the label.
        float elbowX = row.x - k_Indent * 0.5f;

        var color = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.18f)
            : new Color(0f, 0f, 0f, 0.20f);

        // Rails for the ancestors above the parent. Unity only calls us for rows
        // it actually paints, so a full-height rail stays correct while a branch
        // is collapsed.
        for (int up = 1; up <= depth - 1; up++)
        {
            float railX = elbowX - k_Indent * up;
            EditorGUI.DrawRect(new Rect(railX, row.y, k_LineWidth, row.height), color);
        }

        float elbowY = row.y + row.height * 0.5f;

        // Upper arm is always drawn; the lower arm only when a sibling follows,
        // which is what turns a plain rail into a "last child" corner.
        EditorGUI.DrawRect(new Rect(elbowX, row.y, k_LineWidth, elbowY - row.y), color);

        if (!IsLastSibling(t))
            EditorGUI.DrawRect(new Rect(elbowX, elbowY, k_LineWidth, row.yMax - elbowY), color);

        // Horizontal stub pointing at this row's own foldout gutter.
        EditorGUI.DrawRect(new Rect(elbowX, elbowY, k_StubLength, k_LineWidth), color);
    }

    static bool IsLastSibling(Transform t)
    {
        // Scene roots report their index in the scene's root list, and
        // scene.rootCount is the sibling count — same rule as a normal child.
        if (t.parent != null)
            return t.GetSiblingIndex() == t.parent.childCount - 1;

        var scene = t.gameObject.scene;
        return t.GetSiblingIndex() == scene.rootCount - 1;
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
        var box = IconBox(row, size);

        var current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && box.Contains(current.mousePosition))
        {
            // Consume the click so the row neither selects nor starts a drag.
            current.Use();
            SetActive(target, !target.activeSelf);
        }

        DrawIcon(box, target.activeSelf);
    }

    /// <summary>
    /// Where the eye sits for a row. row.xMax includes Unity's vertical scrollbar
    /// gutter, so reserve that width or the icon gets clipped at the panel edge.
    /// </summary>
    static Rect IconBox(Rect row, float size)
    {
        float right = row.xMax - k_ScrollbarWidth - k_RightPad;
        float h = size * (k_EyeOnUv.height / k_EyeOnUv.width);
        return new Rect(right - size, row.y + (row.height - h) * 0.5f, size, h);
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

        // Both PNGs are 32x32 with the glyph squeezed into a small middle band,
        // so draw only that band instead of the whole padded quad.
        var uv = active ? k_EyeOnUv : k_EyeOffUv;
        var quad = new Rect(box.x, box.y, box.width, box.width * (uv.height / uv.width));

        // Icons are solid white; tint so they read on both editor skins.
        var previous = GUI.color;
        GUI.color = EditorGUIUtility.isProSkin
            ? new Color(0.82f, 0.82f, 0.82f)
            : new Color(0.28f, 0.28f, 0.28f);
        GUI.DrawTextureWithTexCoords(quad, icon, uv, true);
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
