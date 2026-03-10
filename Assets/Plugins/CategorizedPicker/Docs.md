# CategorizedSpritePicker — Documentation

A Unity Editor extension that replaces the default Sprite object field with a categorized, searchable picker window. Sprites are automatically organized into categories based on your project's folder hierarchy, making it fast to find and assign sprites without scrolling through a flat list.

---

## Overview

When you mark a `Sprite` field with the `[CategorizedSprite]` attribute, the Inspector renders a custom field with a **Pick** button. Clicking it opens the picker window, where sprites are grouped by folder path, filterable by category, and searchable by name.

```
Assets/
└── Sprites/
    ├── Characters/
    │   ├── Heroes/   → Main: "Characters"  Sub: "Heroes"
    │   └── Enemies/  → Main: "Characters"  Sub: "Enemies"
    └── UI/
        └── Buttons/  → Main: "UI"          Sub: "Buttons"
```

---

## Installation

The plugin is a drop-in package. No additional setup is required — just ensure the files are inside your project's `Assets/` folder under any path.

**Required files:**

| File | Purpose |
|---|---|
| `CategorizedSpriteAttribute.cs` | Runtime attribute (no Editor dependency) |
| `Editor/CategorizedSpriteDrawer.cs` | Custom property drawer |
| `Editor/CategorizedPicker.cs` | Picker `EditorWindow` |

---

## Usage

### 1. Add the attribute to a Sprite field

```csharp
using UnityEngine;
using RookieDev0.CategorizedPicker;

public class MyCharacter : MonoBehaviour
{
    [CategorizedSprite]
    public Sprite icon;

    [CategorizedSprite]
    public Sprite attackEffect;
}
```

The `[CategorizedSprite]` attribute works on any `Sprite` field in a `MonoBehaviour` or `ScriptableObject`.

### 2. Open the picker

In the Inspector, click the **Pick** button next to the field. The picker window opens, loaded with all sprites found under the configured base path.

### 3. Select a sprite

- Browse by main category (dropdown) and sub-category (tag strip).
- Or type in the search bar to filter by sprite name across all categories.
- Click a sprite card to assign it to the field and close the window.

Clicking a card also **pings** the sprite asset in the Project window.

---

## Picker Window

```
┌─────────────────────────────────────────────────────┐
│ [SPRITES]  [Main Category ▾]  [Search...     ]  [⚙] │  ← Toolbar
├─────────────────────────────────────────────────────┤
│  [All]  [📁 Heroes]  [📁 Enemies]  [📁 Bosses]      │  ← Sub-category strip
├─────────────────────────────────────────────────────┤
│  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐            │
│  │ img  │  │ img  │  │ img  │  │ img  │            │  ← Sprite grid
│  │ name │  │ name │  │ name │  │ name │            │
│  └──────┘  └──────┘  └──────┘  └──────┘            │
└─────────────────────────────────────────────────────┘
```

### Toolbar

| Control | Description |
|---|---|
| Main Category dropdown | Filters the grid to a top-level folder group. Select **All** to show every sprite. |
| Search field | Real-time name filter across all categories. Clears the sub-category strip while active. |
| ⚙ Settings button | Toggles the settings panel below the toolbar. |

### Sub-category strip

Appears when a specific main category is selected. Each button represents one level of sub-folder depth. The active button is highlighted in blue. Selecting **All** shows every sprite in the current main category regardless of sub-folder.

### Sprite grid

Each card displays:

- A **preview thumbnail** (uses `AssetPreview`; falls back to the raw texture).
- The **sprite name** underneath, truncated with an ellipsis if too long (full name visible on hover tooltip).

Cards highlight on hover. Click to assign.

---

## Settings Panel

Open via the ⚙ button in the toolbar. Changes take effect after clicking **↺ Apply & Refresh**.

| Setting | Type | Default | Description |
|---|---|---|---|
| **Base Path** | string | `Assets/Sprites` | The root folder to scan for sprites. If the path does not exist, the entire project is scanned. |
| **Ignore Levels** | int (≥ 0) | `0` | Number of folder levels to skip from the top of the path before building categories. Useful when sprites live inside a shared parent folder you don't want as a category. |
| **Category Depth** | int (≥ 1) | `1` | How many folder levels (after ignored levels) form the **main category** label. Levels beyond this depth become sub-categories. |

### Example: Ignore Levels & Category Depth

Given the path `Assets/Sprites/Pack01/Characters/Heroes/`:

| Ignore Levels | Category Depth | Main Category | Sub-category |
|---|---|---|---|
| 0 | 1 | `Pack01` | `Characters/Heroes` |
| 1 | 1 | `Characters` | `Heroes` |
| 1 | 2 | `Characters > Heroes` | *(Root)* |

All settings are persisted in `EditorPrefs` and restored automatically when the window is reopened.

---

## How Categories Are Built

On load, the picker calls `AssetDatabase.FindAssets("t:Sprite", ...)` under the base path, then for each sprite:

1. Strips the base path prefix to get a relative path.
2. Splits the directory into folder segments.
3. Skips the first `ignoreLevels` segments.
4. Takes the next `categoryDepth` segments joined with ` > ` as the **main category**.
5. Any remaining segments joined with `/` become the **sub-category**.
6. Sprites at the root level (no sub-folder) are placed under `Uncategorized / Root`.

---

## Persistence

The picker remembers the following between sessions via `EditorPrefs`:

- Base path, ignore levels, category depth (settings)
- Last selected main category
- Last selected sub-category

---

## API Reference

### `CategorizedSpriteAttribute`

```csharp
namespace RookieDev0.CategorizedPicker

public class CategorizedSpriteAttribute : PropertyAttribute { }
```

Apply to any `public Sprite` or serialized `Sprite` field. No constructor arguments.

### `CategorizedSpritePicker`

```csharp
public static void ShowWindow(SerializedProperty property)
```

Opens (or focuses) the picker window and binds it to `property`. Called automatically by the drawer when the Pick button is clicked.

---

## Requirements

- Unity **2021.2** or later (UIElements / UI Toolkit required for the custom drawer).
- Works in both **Built-in** and **URP / HDRP** projects.
- Editor-only — `CategorizedSpriteAttribute` carries no runtime overhead; the drawer and picker window exist only inside the `Editor` folder.
