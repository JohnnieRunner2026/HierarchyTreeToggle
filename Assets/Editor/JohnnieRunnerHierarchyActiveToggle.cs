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
    // Unity 2020.3 indents one hierarchy step by 14px and draws a row's foldout
    // arrow in the 14px cell immediately left of the row rect (about
    // row.x-11 .. row.x-3). Putting the guide column 21px left of the row lines
    // it up with the centre of the *parent's* arrow cell, so the branch visibly
    // hangs from the parent object instead of hugging the child's own arrow.
    const float k_Indent = 14f;
    const float k_GuideOffset = 21f;
    const float k_LineWidth = 1f;

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

        // row.x is where the row's own content starts; one indent to the left is
        // the cell that holds this row's foldout arrow (if it has children).
        float elbowX = row.x - k_GuideOffset;
        bool hasFoldout = t.childCount > 0;

        var color = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.18f)
            : new Color(0f, 0f, 0f, 0.20f);

        // Rails for every ancestor column. The visual indent has one extra step
        // on top of the transform depth (the Hierarchy's scene-name pseudo row),
        // so the loop has to run up to depth *inclusive* — otherwise the trunk
        // stops dead at a parent row and leaves a dangling stub when that parent
        // is expanded. Unity only calls us for rows it actually paints, so a
        // full-height rail stays correct while a branch is collapsed.
        for (int up = 1; up <= depth; up++)
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

        // Horizontal stub pointing at the row. Rows that own a foldout stop the
        // arm just before their own arrow glyph; leaves run all the way to the label.
        float arm = hasFoldout ? k_GuideOffset - k_Indent + 2f : k_GuideOffset;
        EditorGUI.DrawRect(new Rect(elbowX, elbowY, arm, k_LineWidth), color);
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
