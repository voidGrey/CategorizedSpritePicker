
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class CategorizedSpritePicker : EditorWindow
{
    // ── EditorPrefs keys ─────────────────────────────────────────────────────
    private const string PrefsBasePath     = "SmartSpritePicker_BasePath";
    private const string PrefsIgnoreLevels = "SmartSpritePicker_IgnoreLevels";
    private const string PrefsCategoryDepth= "SmartSpritePicker_CategoryDepth";
    private const string PrefsMainCategory = "SmartSpritePicker_MainCategory";
    private const string PrefsSubCategory  = "SmartSpritePicker_SubCategory";

    // ── Palette ───────────────────────────────────────────────────────────────
    static readonly Color ColBg          = new Color(0.13f, 0.13f, 0.13f);
    static readonly Color ColPanelBg     = new Color(0.10f, 0.10f, 0.10f);
    static readonly Color ColToolbarBg   = new Color(0.12f, 0.12f, 0.12f);
    static readonly Color ColBorder      = new Color(0.22f, 0.22f, 0.22f);
    static readonly Color ColSeparator   = new Color(0.18f, 0.18f, 0.18f);
    static readonly Color ColLabel       = new Color(0.55f, 0.55f, 0.55f);
    static readonly Color ColValue       = new Color(0.88f, 0.88f, 0.88f);
    // Amber — settings / action chips
    static readonly Color ColAmber       = new Color(0.78f, 0.55f, 0.16f);
    static readonly Color ColAmberDim    = new Color(0.63f, 0.43f, 0.16f);
    static readonly Color ColAmberText   = new Color(0.90f, 0.78f, 0.47f);
    static readonly Color ColAmberChipBg = new Color(0.18f, 0.14f, 0.07f);
    // Blue — active sub-category button
    static readonly Color ColBlue        = new Color(0.35f, 0.65f, 0.95f);
    static readonly Color ColBlueBg      = new Color(0.07f, 0.12f, 0.20f);
    static readonly Color ColBlueBrd     = new Color(0.15f, 0.28f, 0.48f);
    // Cyan — "All" state label
    static readonly Color ColCyan        = new Color(0.45f, 0.80f, 0.95f);
    static readonly Color ColCyanBg      = new Color(0.06f, 0.14f, 0.20f);
    // Sprite card
    static readonly Color ColCardBg      = new Color(0.15f, 0.15f, 0.15f);
    static readonly Color ColCardHover   = new Color(0.10f, 0.18f, 0.28f);
    static readonly Color ColCardBrd     = new Color(0.22f, 0.22f, 0.22f);
    static readonly Color ColCardBrdHov  = new Color(0.25f, 0.50f, 0.80f);

    // ── State ─────────────────────────────────────────────────────────────────
    private SerializedProperty _targetProperty;
    private string _basePath             = "Assets/Sprites";
    private int    _ignoreLevels         = 0;
    private int    _categoryDepth        = 1;
    private string _searchQuery          = "";
    private string _currentMainCategory  = "All";
    private string _currentSubCategory   = "All";

    private Dictionary<string, Dictionary<string, List<Sprite>>> _hierarchy = new();
    private List<Sprite> _allSprites = new();

    // ── UI refs ───────────────────────────────────────────────────────────────
    private DropdownField      _mainCategoryDropdown;
    private ScrollView         _gridScrollView;
    private ToolbarSearchField _searchField;
    private VisualElement      _subCategoryContainer;
    private VisualElement      _gridContainer;
    private VisualElement      _settingsPanel;
    private bool               _isSettingsOpen = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        _basePath            = EditorPrefs.GetString(PrefsBasePath,     "Assets/Sprites");
        _ignoreLevels        = EditorPrefs.GetInt   (PrefsIgnoreLevels, 0);
        _categoryDepth       = EditorPrefs.GetInt   (PrefsCategoryDepth, 1);
        _currentMainCategory = EditorPrefs.GetString(PrefsMainCategory, "All");
        _currentSubCategory  = EditorPrefs.GetString(PrefsSubCategory,  "All");
    }

    // ── Entry point ───────────────────────────────────────────────────────────
    public static void ShowWindow(SerializedProperty property)
    {
        var window = GetWindow<CategorizedSpritePicker>("Sprite Picker");
        window._targetProperty = property;
        window.minSize = new Vector2(540, 440);
        window.LoadSprites();
        window.Show();
    }

    // ── CreateGUI ─────────────────────────────────────────────────────────────
    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.flexDirection   = FlexDirection.Column;
        root.style.backgroundColor = ColBg;

        // ── 1. Top toolbar ────────────────────────────────────────────────────
        var topBar = new VisualElement();
        topBar.style.flexDirection     = FlexDirection.Row;
        topBar.style.alignItems        = Align.Center;
        topBar.style.backgroundColor   = ColToolbarBg;
        topBar.style.borderBottomWidth = 1;
        topBar.style.borderBottomColor = ColBorder;
        topBar.style.paddingLeft       = 8;
        topBar.style.paddingRight      = 6;
        topBar.style.paddingTop        = 4;
        topBar.style.paddingBottom     = 4;
        topBar.style.height            = 32;

        // "SPRITES" title chip
        var titleChip = MakeChip("SPRITES", ColAmberChipBg, ColAmberDim, 9);
        titleChip.style.marginRight = 8;
        titleChip.style.flexShrink  = 0;
        topBar.Add(titleChip);

        // Main category dropdown
        _mainCategoryDropdown = new DropdownField();
        _mainCategoryDropdown.style.width     = 200;
        _mainCategoryDropdown.style.flexShrink = 0;
        _mainCategoryDropdown.style.marginRight = 6;
        _mainCategoryDropdown.RegisterValueChangedCallback(evt =>
        {
            _currentMainCategory = evt.newValue;
            EditorPrefs.SetString(PrefsMainCategory, _currentMainCategory);
            UpdateSubCategoryToolbar();
        });
        topBar.Add(_mainCategoryDropdown);

        // Search field
        _searchField = new ToolbarSearchField();
        _searchField.style.flexGrow = 1;
        _searchField.RegisterValueChangedCallback(evt =>
        {
            _searchQuery = evt.newValue.ToLower();
            UpdateSubCategoryToolbar();
            RefreshGrid();
        });
        topBar.Add(_searchField);

        // Settings toggle button
        Texture gearIcon = EditorGUIUtility.IconContent("d_Settings").image;
        var settingsBtn = new Button(() =>
        {
            _isSettingsOpen = !_isSettingsOpen;
            _settingsPanel.style.display = _isSettingsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        });
        settingsBtn.style.width           = 26;
        settingsBtn.style.height          = 22;
        settingsBtn.style.marginLeft      = 4;
        settingsBtn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        settingsBtn.style.borderTopWidth  = settingsBtn.style.borderBottomWidth =
        settingsBtn.style.borderLeftWidth = settingsBtn.style.borderRightWidth  = 0;
        settingsBtn.tooltip = "Settings";
        settingsBtn.Add(new Image { image = gearIcon, style = { width = 16, height = 16, alignSelf = Align.Center } });
        topBar.Add(settingsBtn);

        root.Add(topBar);

        // ── 2. Settings panel (hidden by default) ────────────────────────────
        _settingsPanel = new VisualElement();
        _settingsPanel.style.display          = DisplayStyle.None;
        _settingsPanel.style.backgroundColor  = new Color(0.09f, 0.09f, 0.09f);
        _settingsPanel.style.borderBottomWidth = 1;
        _settingsPanel.style.borderBottomColor = ColBorder;
        _settingsPanel.style.paddingTop        = 8;
        _settingsPanel.style.paddingBottom     = 10;
        _settingsPanel.style.paddingLeft       = 10;
        _settingsPanel.style.paddingRight      = 10;

        var settingsHeader = MakeChip("SETTINGS", ColAmberChipBg, ColAmberDim, 9);
        settingsHeader.style.alignSelf   = Align.FlexStart;
        settingsHeader.style.marginBottom = 8;
        _settingsPanel.Add(settingsHeader);

        var basePathRow = MakeSettingsRow("Base Path");
        var basePathField = new TextField { value = _basePath };
        basePathField.style.flexGrow = 1;
        StyleSettingsField(basePathField);
        basePathField.RegisterValueChangedCallback(evt =>
        {
            _basePath = evt.newValue;
            EditorPrefs.SetString(PrefsBasePath, _basePath);
        });
        basePathRow.Add(basePathField);
        _settingsPanel.Add(basePathRow);

        var ignoreLevRow = MakeSettingsRow("Ignore Levels");
        var ignoreLevField = new IntegerField { value = _ignoreLevels };
        ignoreLevField.style.width = 60;
        StyleSettingsField(ignoreLevField);
        ignoreLevField.RegisterValueChangedCallback(evt =>
        {
            _ignoreLevels = Mathf.Max(0, evt.newValue);
            ignoreLevField.value = _ignoreLevels;
            EditorPrefs.SetInt(PrefsIgnoreLevels, _ignoreLevels);
        });
        ignoreLevRow.Add(ignoreLevField);
        _settingsPanel.Add(ignoreLevRow);

        var catDepthRow = MakeSettingsRow("Category Depth");
        var catDepthField = new IntegerField { value = _categoryDepth };
        catDepthField.style.width = 60;
        StyleSettingsField(catDepthField);
        catDepthField.RegisterValueChangedCallback(evt =>
        {
            _categoryDepth = Mathf.Max(1, evt.newValue);
            catDepthField.value = _categoryDepth;
            EditorPrefs.SetInt(PrefsCategoryDepth, _categoryDepth);
        });
        catDepthRow.Add(catDepthField);
        _settingsPanel.Add(catDepthRow);

        var refreshBtn = MakeActionButton("↺  Apply & Refresh", ColAmberChipBg, ColAmber);
        refreshBtn.style.marginTop  = 8;
        refreshBtn.style.alignSelf  = Align.FlexEnd;
        refreshBtn.clicked += () =>
        {
            _searchQuery = "";
            if (_searchField != null) _searchField.value = "";
            LoadSprites();
        };
        _settingsPanel.Add(refreshBtn);

        root.Add(_settingsPanel);

        // ── 3. Sub-category tag strip ─────────────────────────────────────────
        _subCategoryContainer = new VisualElement();
        _subCategoryContainer.style.flexDirection    = FlexDirection.Row;
        _subCategoryContainer.style.flexWrap         = Wrap.Wrap;
        _subCategoryContainer.style.paddingTop        = 5;
        _subCategoryContainer.style.paddingBottom     = 5;
        _subCategoryContainer.style.paddingLeft       = 6;
        _subCategoryContainer.style.paddingRight      = 6;
        _subCategoryContainer.style.borderBottomWidth = 1;
        _subCategoryContainer.style.borderBottomColor = ColBorder;
        _subCategoryContainer.style.backgroundColor   = new Color(0.11f, 0.11f, 0.11f);
        root.Add(_subCategoryContainer);

        // ── 4. Grid scroll view ───────────────────────────────────────────────
        _gridScrollView = new ScrollView(ScrollViewMode.Vertical);
        _gridScrollView.style.flexGrow        = 1;
        _gridScrollView.style.backgroundColor = ColBg;

        _gridContainer = new VisualElement();
        _gridContainer.style.flexDirection = FlexDirection.Row;
        _gridContainer.style.flexWrap      = Wrap.Wrap;
        _gridContainer.style.paddingTop    = 8;
        _gridContainer.style.paddingBottom = 8;
        _gridContainer.style.paddingLeft   = 8;
        _gridContainer.style.paddingRight  = 8;

        _gridScrollView.Add(_gridContainer);
        root.Add(_gridScrollView);

        if (_hierarchy.Count > 0)
            UpdateMainCategoryDropdown();
    }

    // ── Data loading ──────────────────────────────────────────────────────────
    private void LoadSprites()
    {
        _hierarchy.Clear();
        _allSprites.Clear();

        string normalizedBasePath = _basePath.Replace("\\", "/").TrimEnd('/');
        string[] guids;

        if (AssetDatabase.IsValidFolder(_basePath))
        {
            guids = AssetDatabase.FindAssets("t:Sprite", new[] { _basePath });
        }
        else
        {
            Debug.LogWarning($"[SpritePicker] Base path '{_basePath}' not found — searching entire project.");
            guids = AssetDatabase.FindAssets("t:Sprite");
            normalizedBasePath = "Assets";
        }

        foreach (var guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            _allSprites.Add(sprite);

            string relativePath = path;
            if (path.StartsWith(normalizedBasePath))
            {
                relativePath = path.Substring(normalizedBasePath.Length);
                if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
            }

            string directory    = Path.GetDirectoryName(relativePath)?.Replace("\\", "/") ?? "";
            string mainCategory = "Uncategorized";
            string subCategory  = "Root";

            if (!string.IsNullOrEmpty(directory))
            {
                var parts = directory.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > _ignoreLevels)
                {
                    var activeParts = parts.Skip(_ignoreLevels).ToArray();
                    if (activeParts.Length > 0)
                    {
                        mainCategory = string.Join(" > ", activeParts.Take(_categoryDepth));
                        if (activeParts.Length > _categoryDepth)
                            subCategory = string.Join("/", activeParts.Skip(_categoryDepth));
                    }
                }
                else if (parts.Length > 0)
                {
                    mainCategory = string.Join(" > ", parts);
                }
            }

            if (!_hierarchy.TryGetValue(mainCategory, out var subDict))
            {
                subDict = new Dictionary<string, List<Sprite>>();
                _hierarchy[mainCategory] = subDict;
            }
            if (!subDict.TryGetValue(subCategory, out var spriteList))
            {
                spriteList = new List<Sprite>();
                subDict[subCategory] = spriteList;
            }
            if (!spriteList.Contains(sprite))
                spriteList.Add(sprite);
        }

        if (rootVisualElement != null)
            UpdateMainCategoryDropdown();
    }

    // ── Category dropdown ─────────────────────────────────────────────────────
    private void UpdateMainCategoryDropdown()
    {
        if (_mainCategoryDropdown == null) return;

        var mainCats = _hierarchy.Keys.OrderBy(k => k).ToList();
        if (mainCats.Count == 0) mainCats.Add("No Categories");
        mainCats.Insert(0, "All");

        _mainCategoryDropdown.choices = mainCats;

        if (mainCats.Contains(_currentMainCategory))
            _mainCategoryDropdown.value = _currentMainCategory;
        else
        {
            _mainCategoryDropdown.index  = 0;
            _currentMainCategory         = "All";
            EditorPrefs.SetString(PrefsMainCategory, _currentMainCategory);
        }

        UpdateSubCategoryToolbar();
    }

    // ── Sub-category strip ────────────────────────────────────────────────────
    private void UpdateSubCategoryToolbar()
    {
        if (_subCategoryContainer == null) return;
        _subCategoryContainer.Clear();

        // ── Search mode ──────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            var chip = MakeChip($"  SEARCH  \"{_searchQuery}\"  ", ColAmberChipBg, ColAmberText, 10);
            chip.style.marginTop    = 3;
            chip.style.marginBottom = 3;
            _subCategoryContainer.Add(chip);
            RefreshGrid();
            return;
        }

        // ── All-categories mode ──────────────────────────────────────────────
        if (_currentMainCategory == "All")
        {
            var chip = MakeChip("ALL SPRITES", ColCyanBg, ColCyan, 10);
            chip.style.marginTop    = 3;
            chip.style.marginBottom = 3;
            _subCategoryContainer.Add(chip);
            RefreshGrid();
            return;
        }

        if (!_hierarchy.TryGetValue(_currentMainCategory, out var subDict)) return;

        var subCategories = subDict.Keys.OrderBy(k => k).ToList();
        subCategories.Insert(0, "All");

        if (!subCategories.Contains(_currentSubCategory))
        {
            _currentSubCategory = "All";
            EditorPrefs.SetString(PrefsSubCategory, _currentSubCategory);
        }

        Texture folderIcon = EditorGUIUtility.IconContent("Folder Icon").image;

        foreach (var subCat in subCategories)
        {
            var capturedCat = subCat;
            bool isAll      = subCat == "All";

            var btn = new Button(() =>
            {
                _currentSubCategory = capturedCat;
                EditorPrefs.SetString(PrefsSubCategory, _currentSubCategory);
                SelectSubCategoryButton(capturedCat);
                RefreshGrid();
            });

            btn.name    = $"subcat_{subCat}";
            btn.tooltip = isAll ? "All items in this category" : subCat;

            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems    = Align.Center;
            btn.style.paddingLeft   = isAll ? 10 : 8;
            btn.style.paddingRight  = 10;
            btn.style.paddingTop    = 3;
            btn.style.paddingBottom = 3;
            btn.style.marginTop     = 3;
            btn.style.marginBottom  = 3;
            btn.style.marginLeft    = 3;
            btn.style.marginRight   = 3;
            btn.style.borderTopLeftRadius     = btn.style.borderTopRightRadius    =
            btn.style.borderBottomLeftRadius  = btn.style.borderBottomRightRadius = 4;
            btn.style.borderTopWidth = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth = btn.style.borderRightWidth = 1;
            btn.style.borderTopColor = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor = ColBorder;
            btn.style.backgroundColor = ColPanelBg;

            if (!isAll)
            {
                var icon = new Image { image = folderIcon };
                icon.style.width       = 14;
                icon.style.height      = 14;
                icon.style.marginRight = 5;
                btn.Add(icon);
            }

            string displayName = isAll ? "All" : (string.IsNullOrEmpty(subCat) ? "Root" : subCat);
            var lbl = new Label(displayName);
            lbl.style.fontSize       = 11;
            lbl.style.color          = ColLabel;
            lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
            btn.Add(lbl);

            _subCategoryContainer.Add(btn);
        }

        SelectSubCategoryButton(_currentSubCategory);
        RefreshGrid();
    }

    private void SelectSubCategoryButton(string selectedCat)
    {
        if (_subCategoryContainer == null) return;

        foreach (var child in _subCategoryContainer.Children())
        {
            if (!(child is Button btn)) continue;
            bool active = btn.name == $"subcat_{selectedCat}";

            btn.style.backgroundColor = active ? ColBlueBg  : ColPanelBg;
            btn.style.borderTopColor  = btn.style.borderBottomColor =
            btn.style.borderLeftColor = btn.style.borderRightColor  =
                active ? ColBlueBrd : ColBorder;

            var lbl = btn.Q<Label>();
            if (lbl != null) lbl.style.color = active ? ColBlue : ColLabel;
        }
    }

    // ── Grid ──────────────────────────────────────────────────────────────────
    private void RefreshGrid()
    {
        if (_gridContainer == null) return;
        _gridContainer.Clear();

        bool hasSearch = !string.IsNullOrEmpty(_searchQuery);
        IEnumerable<Sprite> spritesToDraw;

        if (hasSearch)
        {
            spritesToDraw = _allSprites.Where(s => s.name.ToLower().Contains(_searchQuery));
        }
        else if (_currentMainCategory == "All")
        {
            spritesToDraw = _allSprites;
        }
        else
        {
            if (!_hierarchy.TryGetValue(_currentMainCategory, out var subDict)) return;
            spritesToDraw = _currentSubCategory == "All"
                ? subDict.Values.SelectMany(l => l)
                : subDict.TryGetValue(_currentSubCategory, out var list) ? list : Enumerable.Empty<Sprite>();
        }

        foreach (var sprite in spritesToDraw)
        {
            var capturedSprite = sprite;

            // ── Card ─────────────────────────────────────────────────────────
            var card = new VisualElement();
            card.style.width              = 84;
            card.style.height             = 108;
            card.style.marginBottom       = 5;
            card.style.marginRight        = 5;
            card.style.marginLeft         = 5;
            card.style.marginRight        = 5;
            card.style.alignItems         = Align.Center;
            card.style.justifyContent     = Justify.Center;
            card.style.backgroundColor    = ColCardBg;
            card.style.paddingTop         = 6;
            card.style.paddingBottom      = 4;
            card.style.paddingLeft        = 4;
            card.style.paddingRight       = 4;
            card.style.borderTopLeftRadius    = card.style.borderTopRightRadius    =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6;
            card.style.borderTopWidth  = card.style.borderBottomWidth =
            card.style.borderLeftWidth = card.style.borderRightWidth  = 1;
            card.style.borderTopColor  = card.style.borderBottomColor =
            card.style.borderLeftColor = card.style.borderRightColor  = ColCardBrd;

            // ── Sprite image ─────────────────────────────────────────────────
            var img = new Image();
            img.image       = AssetPreview.GetAssetPreview(sprite) ?? sprite.texture;
            img.style.width = img.style.height = 64;
            img.scaleMode   = ScaleMode.ScaleToFit;

            // ── Name label ───────────────────────────────────────────────────
            var lbl = new Label(sprite.name);
            lbl.tooltip                = sprite.name;
            lbl.style.unityTextAlign   = TextAnchor.MiddleCenter;
            lbl.style.fontSize         = 9;
            lbl.style.color            = ColLabel;
            lbl.style.whiteSpace       = WhiteSpace.NoWrap;
            lbl.style.overflow         = Overflow.Hidden;
            lbl.style.textOverflow     = TextOverflow.Ellipsis;
            lbl.style.width            = 76;
            lbl.style.marginTop        = 4;

            card.Add(img);
            card.Add(lbl);

            // ── Hover ────────────────────────────────────────────────────────
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.backgroundColor = ColCardHover;
                card.style.borderTopColor  = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor  = ColCardBrdHov;
                lbl.style.color = ColValue;
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.backgroundColor = ColCardBg;
                card.style.borderTopColor  = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor  = ColCardBrd;
                lbl.style.color = ColLabel;
            });

            // ── Click — assign & close ────────────────────────────────────────
            card.RegisterCallback<MouseDownEvent>(_ =>
            {
                EditorGUIUtility.PingObject(capturedSprite);
                if (_targetProperty != null)
                {
                    _targetProperty.objectReferenceValue = capturedSprite;
                    _targetProperty.serializedObject.ApplyModifiedProperties();
                }
                Close();
            });

            _gridContainer.Add(card);
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────
    static Label MakeChip(string text, Color bg, Color col, int size = 10)
    {
        var l = new Label(text);
        l.style.color                   = col;
        l.style.fontSize                = size;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.backgroundColor         = bg;
        l.style.borderTopLeftRadius     = l.style.borderTopRightRadius    =
        l.style.borderBottomLeftRadius  = l.style.borderBottomRightRadius = 3;
        l.style.paddingLeft             = l.style.paddingRight  = 6;
        l.style.paddingTop              = l.style.paddingBottom = 2;
        l.style.unityTextAlign          = TextAnchor.MiddleCenter;
        return l;
    }

    static VisualElement MakeSettingsRow(string labelText)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.marginBottom  = 4;

        var lbl = new Label(labelText);
        lbl.style.color          = ColLabel;
        lbl.style.fontSize       = 11;
        lbl.style.minWidth       = 110;
        lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(lbl);
        return row;
    }

    static void StyleSettingsField(VisualElement field)
    {
        field.style.height = 20;
        field.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            var ti = field.Q<VisualElement>("unity-text-input");
            if (ti == null) return;
            ti.style.backgroundColor  = new Color(0.08f, 0.08f, 0.08f);
            ti.style.borderTopWidth   = ti.style.borderBottomWidth =
            ti.style.borderLeftWidth  = ti.style.borderRightWidth  = 1;
            ti.style.borderTopColor   = ti.style.borderBottomColor =
            ti.style.borderLeftColor  = ti.style.borderRightColor  = ColBorder;
            ti.style.color     = ColValue;
            ti.style.fontSize  = 11;
        });
    }

    static Button MakeActionButton(string text, Color bg, Color col)
    {
        var btn = new Button { text = text };
        btn.style.backgroundColor         = bg;
        btn.style.color                   = col;
        btn.style.fontSize                = 10;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.borderTopLeftRadius     = btn.style.borderTopRightRadius    =
        btn.style.borderBottomLeftRadius  = btn.style.borderBottomRightRadius = 4;
        btn.style.borderTopWidth  = btn.style.borderBottomWidth =
        btn.style.borderLeftWidth = btn.style.borderRightWidth  = 1;
        btn.style.borderTopColor  = btn.style.borderBottomColor =
        btn.style.borderLeftColor = btn.style.borderRightColor  = col;
        btn.style.paddingLeft     = btn.style.paddingRight  = 10;
        btn.style.paddingTop      = btn.style.paddingBottom = 3;
        btn.style.height          = 22;
        return btn;
    }
}
