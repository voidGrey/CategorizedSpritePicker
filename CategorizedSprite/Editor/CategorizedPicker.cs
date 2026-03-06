
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class CategorizedSpritePicker : EditorWindow
{
    private const string PrefsBasePath = "SmartSpritePicker_BasePath";
    private const string PrefsIgnoreLevels = "SmartSpritePicker_IgnoreLevels";
    private const string PrefsCategoryDepth = "SmartSpritePicker_CategoryDepth";
    private const string PrefsMainCategory = "SmartSpritePicker_MainCategory";
    private const string PrefsSubCategory = "SmartSpritePicker_SubCategory";

    private SerializedProperty _targetProperty;
    private string _basePath = "Assets/Sprites";
    private int _ignoreLevels = 0;
    private int _categoryDepth = 1;
    private string _searchQuery = "";

    private void OnEnable()
    {
        _basePath = EditorPrefs.GetString(PrefsBasePath, "Assets/Sprites");
        _ignoreLevels = EditorPrefs.GetInt(PrefsIgnoreLevels, 0);
        _categoryDepth = EditorPrefs.GetInt(PrefsCategoryDepth, 1);
        _currentMainCategory = EditorPrefs.GetString(PrefsMainCategory, "All");
        _currentSubCategory = EditorPrefs.GetString(PrefsSubCategory, "All");
    }

    private string _currentMainCategory = "";
    private string _currentSubCategory = "";

    // Hierarchy: MainCategory -> SubCategory -> List<Sprite>
    private Dictionary<string, Dictionary<string, List<Sprite>>> _hierarchy = new();
    private List<Sprite> _allSprites = new();

    private DropdownField _mainCategoryDropdown;
    private ScrollView _gridScrollView;
    private ToolbarSearchField _searchField;

    private VisualElement _subCategoryContainer;
    private VisualElement _gridContainer;
    private VisualElement _settingsPanel;
    private bool _isSettingsOpen = false;

    public static void ShowWindow(SerializedProperty property)
    {
        var window = GetWindow<CategorizedSpritePicker>("Smart Sprite Picker");
        window._targetProperty = property;
        window.minSize = new Vector2(500, 400);
        window.LoadSprites();
        window.Show();
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.flexDirection = FlexDirection.Column;

        // --- 1. Top Toolbar (Main Category Dropdown, Search & Settings Button) ---
        var topToolbar = new Toolbar();

        _mainCategoryDropdown = new DropdownField();
        _mainCategoryDropdown.style.width = 250;
        _mainCategoryDropdown.RegisterValueChangedCallback(evt => {
            _currentMainCategory = evt.newValue;
            EditorPrefs.SetString(PrefsMainCategory, _currentMainCategory);
            UpdateSubCategoryToolbar();
        });
        topToolbar.Add(_mainCategoryDropdown);

        _searchField = new ToolbarSearchField();
        _searchField.style.flexGrow = 1;
        _searchField.RegisterValueChangedCallback(evt => {
            _searchQuery = evt.newValue.ToLower();
            UpdateSubCategoryToolbar(); // Refresh toolbar to show "Arama Sonuçları" state
            RefreshGrid();
        });
        topToolbar.Add(_searchField);

        // Settings Button
        Texture gearIcon = EditorGUIUtility.IconContent("d_Settings").image;
        var settingsBtn = new ToolbarButton(() => {
            _isSettingsOpen = !_isSettingsOpen;
            _settingsPanel.style.display = _isSettingsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        });
        settingsBtn.Add(new Image { image = gearIcon, style = { width = 16, height = 16, alignSelf = Align.Center } });
        settingsBtn.tooltip = "Settings";
        topToolbar.Add(settingsBtn);

        root.Add(topToolbar);

        // --- Settings Panel (Hidden by Default) ---
        _settingsPanel = new VisualElement();
        _settingsPanel.style.display = DisplayStyle.None;
        _settingsPanel.style.paddingTop = 8;
        _settingsPanel.style.paddingBottom = 8;
        _settingsPanel.style.paddingLeft = 8;
        _settingsPanel.style.paddingRight = 8;
        _settingsPanel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        _settingsPanel.style.borderBottomWidth = 1;
        _settingsPanel.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));

        var basePathField = new TextField("Base Path") { value = _basePath };
        basePathField.RegisterValueChangedCallback(evt => {
            _basePath = evt.newValue;
            EditorPrefs.SetString(PrefsBasePath, _basePath);
        });

        var ignoreLevelsField = new IntegerField("Ignore Levels") { value = _ignoreLevels };
        ignoreLevelsField.RegisterValueChangedCallback(evt => {
            _ignoreLevels = Mathf.Max(0, evt.newValue);
            ignoreLevelsField.value = _ignoreLevels;
            EditorPrefs.SetInt(PrefsIgnoreLevels, _ignoreLevels);
        });

        var categoryDepthField = new IntegerField("Category Depth") { value = _categoryDepth };
        categoryDepthField.RegisterValueChangedCallback(evt => {
            _categoryDepth = Mathf.Max(1, evt.newValue);
            categoryDepthField.value = _categoryDepth;
            EditorPrefs.SetInt(PrefsCategoryDepth, _categoryDepth);
        });

        var refreshBtn = new Button(() => {
            _searchQuery = "";
            if (_searchField != null) _searchField.value = "";
            LoadSprites();
        }) { text = "Apply & Refresh", style = { marginTop = 5 } };

        _settingsPanel.Add(basePathField);
        _settingsPanel.Add(ignoreLevelsField);
        _settingsPanel.Add(categoryDepthField);
        _settingsPanel.Add(refreshBtn);

        root.Add(_settingsPanel);

        // --- 2. Sub Category Container (Flex Wrap enabled for tags) ---
        _subCategoryContainer = new VisualElement();
        _subCategoryContainer.style.flexDirection = FlexDirection.Row;
        _subCategoryContainer.style.flexWrap = Wrap.Wrap; // Adapt to window width
        _subCategoryContainer.style.paddingTop = 4;
        _subCategoryContainer.style.paddingBottom = 4;
        _subCategoryContainer.style.paddingLeft = 4;
        _subCategoryContainer.style.paddingRight = 4;
        _subCategoryContainer.style.borderBottomWidth = 1;
        _subCategoryContainer.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        _subCategoryContainer.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));

        root.Add(_subCategoryContainer);

        // --- 3. Grid ScrollView ---
        _gridScrollView = new ScrollView(ScrollViewMode.Vertical);
        _gridScrollView.style.flexGrow = 1;

        _gridContainer = new VisualElement();
        _gridContainer.style.flexDirection = FlexDirection.Row;
        _gridContainer.style.flexWrap = Wrap.Wrap;
        _gridContainer.style.paddingTop = 5;
        _gridContainer.style.paddingBottom = 5;
        _gridContainer.style.paddingLeft = 5;
        _gridContainer.style.paddingRight = 5;

        _gridScrollView.Add(_gridContainer);
        root.Add(_gridScrollView);

        if (_hierarchy.Count > 0)
        {
            UpdateMainCategoryDropdown();
        }
    }

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
            Debug.LogWarning($"Base path {_basePath} not found. Searching entire project.");
            guids = AssetDatabase.FindAssets("t:Sprite");
            normalizedBasePath = "Assets";
        }

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            _allSprites.Add(sprite);

            string relativePath = path;
            if (path.StartsWith(normalizedBasePath))
            {
                relativePath = path.Substring(normalizedBasePath.Length);
                if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
            }

            string directory = Path.GetDirectoryName(relativePath).Replace("\\", "/");
            string mainCategory = "Uncategorized";
            string subCategory = "Root";

            if (!string.IsNullOrEmpty(directory))
            {
                var parts = directory.Split('/', System.StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > _ignoreLevels)
                {
                    var activeParts = parts.Skip(_ignoreLevels).ToArray();

                    if (activeParts.Length > 0)
                    {
                        var mainParts = activeParts.Take(_categoryDepth).ToArray();
                        mainCategory = string.Join(" > ", mainParts);

                        if (activeParts.Length > _categoryDepth)
                        {
                            var subParts = activeParts.Skip(_categoryDepth).ToArray();
                            subCategory = string.Join("/", subParts);
                        }
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
            {
                spriteList.Add(sprite);
            }
        }

        if (rootVisualElement != null)
        {
            UpdateMainCategoryDropdown();
        }
    }

    private void UpdateMainCategoryDropdown()
    {
        if (_mainCategoryDropdown == null) return;
        var mainCats = _hierarchy.Keys.OrderBy(k => k).ToList();

        if (mainCats.Count == 0) mainCats.Add("No Categories");

        // Global listeleme için en başa 'All' eklendi
        mainCats.Insert(0, "All");

        _mainCategoryDropdown.choices = mainCats;

        if (mainCats.Contains(_currentMainCategory))
        {
            _mainCategoryDropdown.value = _currentMainCategory;
        }
        else
        {
            _mainCategoryDropdown.index = 0;
            _currentMainCategory = "All";
            EditorPrefs.SetString(PrefsMainCategory, _currentMainCategory);
        }

        UpdateSubCategoryToolbar();
    }

    private void UpdateSubCategoryToolbar()
    {
        if (_subCategoryContainer == null) return;
        _subCategoryContainer.Clear();

        // If searching, replace tabs with Search Results label
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            var searchLabel = new Label($"Search Results: '{_searchQuery}'");
            searchLabel.style.paddingLeft = 8;
            searchLabel.style.paddingTop = 8;
            searchLabel.style.paddingBottom = 8;
            searchLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            searchLabel.style.color = new StyleColor(new Color(0.9f, 0.8f, 0.3f)); // Yellowish
            _subCategoryContainer.Add(searchLabel);
            return;
        }

        if (_currentMainCategory == "All")
        {
            var allLabel = new Label("Sprites in All Categories are Listed...");
            allLabel.style.paddingLeft = 8;
            allLabel.style.paddingTop = 8;
            allLabel.style.paddingBottom = 8;
            allLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            allLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 0.9f));
            _subCategoryContainer.Add(allLabel);
            RefreshGrid();
            return;
        }

        if (!_hierarchy.TryGetValue(_currentMainCategory, out var subDict))
            return;

        var subCategories = subDict.Keys.OrderBy(k => k).ToList();

        // Add "All" option for the current main category
        subCategories.Insert(0, "All");

        if (!subCategories.Contains(_currentSubCategory))
        {
            _currentSubCategory = "All";
            EditorPrefs.SetString(PrefsSubCategory, _currentSubCategory);
        }

        Texture folderIcon = EditorGUIUtility.IconContent("Folder Icon").image;

        foreach (var subCat in subCategories)
        {
            var btn = new ToolbarButton(() => {
                _currentSubCategory = subCat;
                EditorPrefs.SetString(PrefsSubCategory, _currentSubCategory);
                SelectSubCategoryButton(subCat);
                RefreshGrid();
            });

            btn.name = $"subcat_{subCat}";
            btn.tooltip = subCat == "All" ? "All Items" : subCat; // Full path Tooltip

            // Daha ferah buton stili
            btn.style.flexDirection = FlexDirection.Row;

            btn.style.alignItems = Align.Center;

            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.paddingTop = 5;
            btn.style.paddingBottom = 5;
            btn.style.marginTop = 4;
            btn.style.marginBottom = 4;
            btn.style.marginLeft = 4;
            btn.style.marginRight = 4;
            btn.style.minWidth = 100;

            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderBottomRightRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderTopLeftRadius = 6;

            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;

            btn.style.borderLeftColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
            btn.style.borderRightColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
            btn.style.borderTopColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
            btn.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));

            if (subCat != "All")
            {
                var icon = new Image { image = folderIcon };
                icon.style.width = 18;
                icon.style.height = 18;
                icon.style.marginRight = 6;
                btn.Add(icon);
            }

            // Tam yollu gösterim korunuyor
            string displayName = subCat == "All" ? "All" : subCat;
            if (string.IsNullOrEmpty(displayName)) displayName = "Root";

            var lbl = new Label(displayName);
            lbl.style.fontSize = 12;
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
            if (child is ToolbarButton btn)
            {
                bool isSelected = btn.name == $"subcat_{selectedCat}";
                if (isSelected)
                {
                    btn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.4f, 0.6f)); // Distinct highlight
                    btn.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.6f, 0.8f));
                    btn.style.borderRightColor = new StyleColor(new Color(0.4f, 0.6f, 0.8f));
                    btn.style.borderTopColor = new StyleColor(new Color(0.4f, 0.6f, 0.8f));
                    btn.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.6f, 0.8f));
                }
                else
                {
                    btn.style.backgroundColor = new StyleColor(StyleKeyword.Null); // Default transparent
                    btn.style.borderLeftColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
                    btn.style.borderRightColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
                    btn.style.borderTopColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
                    btn.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
                }
            }
        }
    }

    private void RefreshGrid()
    {
        if (_gridContainer == null) return;
        _gridContainer.Clear();

        bool hasSearch = !string.IsNullOrEmpty(_searchQuery);
        IEnumerable<Sprite> spritesToDraw;

        // Performans Notu: Binlerce görsel listelendiğinde assetPreview veya grid performansı etkilenebilir. 
        // Ancak AssetPreview cache'li çalıştığı için ListView mimarisi dışında en iyi yöntem budur.
        if (hasSearch)
        {
            spritesToDraw = _allSprites.Where(s => s.name.ToLower().Contains(_searchQuery));
        }
        else if (_currentMainCategory == "All")
        {
            spritesToDraw = _allSprites; // Bütün spritelar
        }
        else
        {
            if (!_hierarchy.TryGetValue(_currentMainCategory, out var subDict)) return;

            if (_currentSubCategory == "All")
            {
                spritesToDraw = subDict.Values.SelectMany(list => list);
            }
            else
            {
                if (!subDict.TryGetValue(_currentSubCategory, out var list)) return;
                spritesToDraw = list;
            }
        }

        foreach (var sprite in spritesToDraw)
        {
            var itemContainer = new VisualElement();
            itemContainer.style.width = 80;
            itemContainer.style.height = 100;
            itemContainer.style.marginRight = 5;
            itemContainer.style.marginBottom = 5;
            itemContainer.style.alignItems = Align.Center;
            itemContainer.style.justifyContent = Justify.Center;

            itemContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            itemContainer.style.paddingTop = 5;
            itemContainer.style.paddingBottom = 5;
            itemContainer.style.paddingLeft = 5;
            itemContainer.style.paddingRight = 5;

            itemContainer.style.borderTopLeftRadius = 5;
            itemContainer.style.borderTopRightRadius = 5;
            itemContainer.style.borderBottomLeftRadius = 5;
            itemContainer.style.borderBottomRightRadius = 5;

            var img = new Image();
            Texture2D tex = AssetPreview.GetAssetPreview(sprite);
            if (tex == null) tex = sprite.texture;

            img.image = tex;
            img.style.width = 64;
            img.style.height = 64;
            img.scaleMode = ScaleMode.ScaleToFit;

            var label = new Label(sprite.name);
            label.tooltip = sprite.name; // Show full name on hover
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = 10;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.width = 70;
            label.style.marginTop = 4;

            itemContainer.Add(img);
            itemContainer.Add(label);

            // Ping on hover
            itemContainer.RegisterCallback<MouseEnterEvent>(evt => {
                itemContainer.style.backgroundColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
            });
            itemContainer.RegisterCallback<MouseLeaveEvent>(evt => {
                itemContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            });

            // Select on click
            itemContainer.RegisterCallback<MouseDownEvent>(evt => {
                EditorGUIUtility.PingObject(sprite);

                if (_targetProperty != null)
                {
                    _targetProperty.objectReferenceValue = sprite;
                    _targetProperty.serializedObject.ApplyModifiedProperties();
                }
                Close();
            });

            _gridContainer.Add(itemContainer);
        }
    }
}
