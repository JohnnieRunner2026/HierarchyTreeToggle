# Hierarchy Tree Toggle

A small editor extension for Unity 2020.3 that adds two things to every row of the Hierarchy window:

- **Tree guide lines** drawn in the indent gutter, so parent/child structure is readable without expanding anything.
- **An eye toggle** on the right edge of each row — one click shows or hides that GameObject.

Editor-only, no runtime overhead, no menu item to configure — drop the folder in and it is already working.

## Features

### Eye toggle
- Open eye = active, crossed-out eye = inactive. Left-click toggles the row's GameObject.
- **Only that object changes.** Children keep their own state, so hiding a parent does not ripple down the tree.
- Every click is a single **undo step** (`Activate` / `Deactivate`) — `Ctrl+Z` reverts it.
- **Prefab-instance aware:** the change is recorded through `PrefabUtility.RecordPrefabInstancePropertyModifications`, so overrides are marked properly and do not silently disappear.
- The click is consumed, so toggling does not select the row or start a drag.
- Tinted for both the dark and the light editor skin.

### Tree guides
- Vertical rails for every ancestor column plus an elbow per row, with a corner on last children.
- Lines stop just short of a parent's foldout arrow instead of crossing it, and line up with the **parent's** column, so a branch visibly hangs from its owner.
- Collapsed branches leave no dangling stubs: Unity only asks for the rows it actually paints, and the rails are drawn per row.
- Drawn with `EditorGUI.DrawRect` during `Repaint` only, so layout and input events pay nothing.

## Install

1. Copy `Assets/Editor/` from this repo into your project's `Assets/Editor/` folder.
2. Switch back to Unity. It compiles, and the toggles are there — nothing else to do.

Keep the folder layout: the icons are resolved by path, so `JohnnieRunnerHierarchyActiveToggle.cs` expects `Assets/Editor/Icons/EyeOn.png` and `Assets/Editor/Icons/EyeOff.png` next to it.

## Compatibility

- Unity **2020.3.x** — measured and tuned on 2020.3.49f1. Editor-only, so the render pipeline does not matter (built-in, URP or HDRP all fine).
- Newer/older versions usually work, but the guide geometry is calibrated to Unity 2020.3's Hierarchy metrics: a 14 px indent step and the foldout arrow living in the 14 px cell left of the row rect. If a version changes that, adjust `k_Indent` / `k_GuideOffset` at the top of the file.

## Usage notes

- Hiding an object means `GameObject.SetActive(false)` — it stops rendering and stops running scripts, exactly like unticking the checkbox in the Inspector.
- The eye is drawn at the row's right edge with the scrollbar gutter reserved, so it never gets clipped when the panel is narrow.
- The Hierarchy's top row (the scene name) is a pseudo item with no GameObject behind it; the tool skips it.

## How it works

```csharp
[InitializeOnLoad]
internal static class JohnnieRunnerHierarchyActiveToggle
{
    static JohnnieRunnerHierarchyActiveToggle()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyRow;
        EditorApplication.hierarchyChanged += s_DepthCache.Clear;
    }
}
```

- `hierarchyWindowItemOnGUI` hands over the row rect but **not** the depth or expanded state, so depth is counted from `transform.parent` and cached per instance ID, cleared on `hierarchyChanged`.
- `row.x` already includes the indent, so one indent step to the left is the cell that holds this row's foldout arrow. The guide column sits 21 px left of `row.x` — the centre of the *parent's* arrow cell.
- `SetActive` is deferred through `EditorApplication.delayCall`, because mutating the hierarchy while Unity is iterating rows to paint them is a good way to get an exception mid-repaint.

## Files

| Path | Purpose |
| --- | --- |
| `Assets/Editor/JohnnieRunnerHierarchyActiveToggle.cs` | The whole tool: guide drawing, eye toggle, undo handling. |
| `Assets/Editor/Icons/EyeOn.png` | Active state icon (32×32, glyph in the middle band). |
| `Assets/Editor/Icons/EyeOff.png` | Inactive state icon. |

## License

No license file yet — all rights reserved. Ask before redistributing.
