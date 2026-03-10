using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RookieDev0.CategorizedPicker
{
    public class CategorizedSpritePicker : EditorWindow
    {
        [SerializeField] private StyleSheet _styleSheet;

        // ── EditorPrefs keys ─────────────────────────────────────────────────────
        private const string PrefsBasePath     = "SmartSpritePicker_BasePath";
        private const string PrefsIgnoreLevels = "SmartSpritePicker_IgnoreLevels";
        private const string PrefsCategoryDepth= "SmartSpritePicker_CategoryDepth";
        private const string PrefsMainCategory = "SmartSpritePicker_MainCategory";
        private const string PrefsSubCategory  = "SmartSpritePicker_SubCategory";
        private const string PrefsCardSize     = "SmartSpritePicker_CardSize";

        // ── Palette ───────────────────────────────────────────────────────────────
        static readonly Color ColBorder      = new Color(0.22f, 0.22f, 0.22f);
        static readonly Color ColValue       = new Color(0.88f, 0.88f, 0.88f);
        // Amber — settings / action chips
        static readonly Color ColAmber       = new Color(0.78f, 0.55f, 0.16f);
        static readonly Color ColAmberDim    = new Color(0.63f, 0.43f, 0.16f);
        static readonly Color ColAmberText   = new Color(0.90f, 0.78f, 0.47f);
        static readonly Color ColAmberChipBg = new Color(0.18f, 0.14f, 0.07f);
        // Cyan — "All" state label
        static readonly Color ColCyan        = new Color(0.45f, 0.80f, 0.95f);
        static readonly Color ColCyanBg      = new Color(0.06f, 0.14f, 0.20f);
        // Error
        static readonly Color ColError       = new Color(0.80f, 0.20f, 0.20f);

        // ── State ─────────────────────────────────────────────────────────────────
        private SerializedProperty _targetProperty;
        private string _basePath             = "Assets/Sprites"; //TODO Generic Yapınca direkt Assets folder'i default seç.
        private int    _ignoreLevels         = 0;
        private int    _categoryDepth        = 1;
        private string _searchQuery          = "";
        private string _currentMainCategory  = "All";
        private string _currentSubCategory   = "All";
        private int    _cardSize             = 84;

        private Dictionary<string, Dictionary<string, List<Sprite>>> _hierarchy = new();
        private List<Sprite> _allSprites = new();

        // ── UI refs ───────────────────────────────────────────────────────────────
        private DropdownField      _mainCategoryDropdown;
        private ScrollView         _gridScrollView;
        private ToolbarSearchField _searchField;
        private VisualElement      _subCategoryContainer;
        private VisualElement      _gridContainer;
        private VisualElement      _settingsPanel;
        private TextField          _basePathField;
        private SliderInt          _cardSizeSlider;
        private bool               _isSettingsOpen      = false;
        private bool               _pathWasInvalid      = false;
        private bool               _needsPreviewRefresh = false;
        private Label              _spriteCountLabel;

        private const string Version = "v1.1";

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            _basePath            = EditorPrefs.GetString(PrefsBasePath,     "Assets/Sprites");
            _ignoreLevels        = EditorPrefs.GetInt   (PrefsIgnoreLevels, 0);
            _categoryDepth       = EditorPrefs.GetInt   (PrefsCategoryDepth, 1);
            _currentMainCategory = EditorPrefs.GetString(PrefsMainCategory, "All");
            _currentSubCategory  = EditorPrefs.GetString(PrefsSubCategory,  "All");
            _cardSize            = EditorPrefs.GetInt   (PrefsCardSize,      84);
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

        // ── Property validity check ───────────────────────────────────────────────
        private bool IsPropertyValid() =>
            _targetProperty != null &&
            _targetProperty.serializedObject != null &&
            _targetProperty.serializedObject.targetObject != null;

        private void OnFocus()
        {
            rootVisualElement?.Focus();
        }

        private void Update()
        {
            if (AssetPreview.IsLoadingAssetPreviews())
                _needsPreviewRefresh = true;
            else if (_needsPreviewRefresh)
            {
                _needsPreviewRefresh = false;
                RefreshGrid();
            }
        }

        // ── CreateGUI ─────────────────────────────────────────────────────────────
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.styleSheets.Add(_styleSheet);
            root.AddToClassList("picker-root");
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) Close();
            });

            // ── 1. Top toolbar ────────────────────────────────────────────────────
            var topBar = new VisualElement();
            topBar.styleSheets.Add(_styleSheet);
            topBar.AddToClassList("picker-top-bar");


            // "SPRITES" title chip
            var titleChip = MakeChip("CATEGORIES", ColAmberChipBg, ColAmberDim, 9);
            titleChip.style.marginRight = 8;
            titleChip.style.flexShrink  = 0;
            topBar.Add(titleChip);

            // Main category dropdown
            _mainCategoryDropdown = new DropdownField();
            _mainCategoryDropdown.styleSheets.Add(_styleSheet);
            _mainCategoryDropdown.AddToClassList("picker-main-category-dropdown");

            _mainCategoryDropdown.RegisterValueChangedCallback(evt =>
            {
                _currentMainCategory = evt.newValue;
                EditorPrefs.SetString(PrefsMainCategory, _currentMainCategory);
                UpdateSubCategoryToolbar();
            });

            topBar.Add(_mainCategoryDropdown);

            // Search field
            _searchField = new ToolbarSearchField();
            _searchField.styleSheets.Add(_styleSheet);
            _searchField.AddToClassList("picker-search-field");
            _searchField.placeholderText = "Search...";

            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue.ToLower();
                UpdateSubCategoryToolbar();
                RefreshGrid();
            });
            topBar.Add(_searchField);

            // "None" clear button
            var colNoneBg = new Color(0.12f, 0.05f, 0.05f);
            var colNone   = new Color(0.80f, 0.30f, 0.30f);
            var noneBtn = MakeActionButton("✕  None", colNoneBg, colNone);
            noneBtn.style.marginRight = 4;
            noneBtn.style.flexShrink  = 0;
            noneBtn.tooltip = "Clear sprite (set to None)";
            noneBtn.clicked += () =>
            {
                if (IsPropertyValid())
                {
                    _targetProperty.objectReferenceValue = null;
                    _targetProperty.serializedObject.ApplyModifiedProperties();
                }
                Close();
            };
            topBar.Add(noneBtn);

            // Settings toggle button
            Texture gearIcon = EditorGUIUtility.IconContent("d_Settings").image;
            var settingsBtn = new Button(() =>
            {
                _isSettingsOpen = !_isSettingsOpen;
                _settingsPanel.style.display = _isSettingsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            });
            settingsBtn.styleSheets.Add(_styleSheet);
            settingsBtn.AddToClassList("picker-settings-btn");

            settingsBtn.tooltip = "Settings";
            settingsBtn.Add(new Image { image = gearIcon, style = { width = 16, height = 16, alignSelf = Align.Center } });
            topBar.Add(settingsBtn);

            root.Add(topBar);

            // ── 2. Settings panel (hidden by default) ────────────────────────────
            _settingsPanel = new VisualElement();
            _settingsPanel.AddToClassList("picker-settings-panel");
            _settingsPanel.style.display = _isSettingsOpen ? DisplayStyle.Flex : DisplayStyle.None;

            var settingsHeader = MakeChip("SETTINGS", ColAmberChipBg, ColAmberDim, 9);
            settingsHeader.style.alignSelf   = Align.FlexStart;
            settingsHeader.style.marginBottom = 8;
            _settingsPanel.Add(settingsHeader);

            var basePathRow = MakeSettingsRow("Base Path");
            var basePathField = new TextField { value = _basePath };
            _basePathField = basePathField;
            basePathField.style.flexGrow = 1;
            StyleSettingsField(basePathField);
            if (_pathWasInvalid)
                basePathField.RegisterCallback<AttachToPanelEvent>(_ =>
                    SetTextInputBorderColor(basePathField, ColError));
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
            _subCategoryContainer.AddToClassList("picker-subcategory-container");
            root.Add(_subCategoryContainer);

            // ── 4. Grid scroll view ───────────────────────────────────────────────
            _gridScrollView = new ScrollView(ScrollViewMode.Vertical);
            _gridScrollView.AddToClassList("picker-grid-scrollview");

            _gridContainer = new VisualElement();
            _gridContainer.AddToClassList("picker-grid-container");

            _gridScrollView.Add(_gridContainer);
            root.Add(_gridScrollView);

            // ── 5. Bottom bar ─────────────────────────────────────────────────────
            var bottomBar = new VisualElement();
            bottomBar.AddToClassList("picker-bottom-bar");

            var versionText = new Label("CategorizedPicker");
            versionText.AddToClassList("picker-version-text");
            bottomBar.Add(versionText);

            var versionChip = MakeChip(Version, ColAmberChipBg, ColAmberDim, 9);
            bottomBar.Add(versionChip);

            _spriteCountLabel = new Label("");
            _spriteCountLabel.AddToClassList("picker-version-text");
            _spriteCountLabel.style.marginLeft = 8;
            bottomBar.Add(_spriteCountLabel);

            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bottomBar.Add(spacer);

            // Card size slider
            var sizeIcon = new Label("⊞");
            sizeIcon.style.color          = Color.white;
            sizeIcon.style.fontSize       = 11;
            sizeIcon.style.marginRight    = 4;
            sizeIcon.style.unityTextAlign = TextAnchor.MiddleLeft;
            bottomBar.Add(sizeIcon);

            _cardSizeSlider = new SliderInt(48, 160);
            _cardSizeSlider.value          = _cardSize;
            _cardSizeSlider.style.width    = 100;
            _cardSizeSlider.style.flexShrink = 0;
            _cardSizeSlider.RegisterValueChangedCallback(evt =>
            {
                _cardSize = evt.newValue;
                EditorPrefs.SetInt(PrefsCardSize, _cardSize);
                RefreshGrid();
            });
            bottomBar.Add(_cardSizeSlider);

            root.Add(bottomBar);

            if (_hierarchy.Count > 0)
                UpdateMainCategoryDropdown();
            else
                LoadSprites();
        }

        // ── Data loading ──────────────────────────────────────────────────────────
        private void LoadSprites()
        {
            _hierarchy.Clear();
            _allSprites.Clear();

            string normalizedBasePath = _basePath.Replace("\\", "/").TrimEnd('/');
            string[] guids;

            _pathWasInvalid = false;
            SetTextInputBorderColor(_basePathField, ColBorder);

            if (AssetDatabase.IsValidFolder(_basePath))
            {
                guids = AssetDatabase.FindAssets("t:Sprite", new[] { _basePath });
            }
            else
            {
                _pathWasInvalid  = true;
                _isSettingsOpen  = true;
                if (_settingsPanel != null) _settingsPanel.style.display = DisplayStyle.Flex;
                SetTextInputBorderColor(_basePathField, ColError);
                guids = AssetDatabase.FindAssets("t:Sprite");
                normalizedBasePath = "Assets";
            }

            foreach (var guid in guids)
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                if (!_allSprites.Contains(sprite))
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
                btn.AddToClassList("picker-subcat-btn");
                if (isAll) btn.AddToClassList("is-all");

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

                if (active) btn.AddToClassList("active");
                else btn.RemoveFromClassList("active");
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
                if (!_hierarchy.TryGetValue(_currentMainCategory, out var subDict))
                {
                    spritesToDraw = Enumerable.Empty<Sprite>();
                }
                else
                {
                    spritesToDraw = _currentSubCategory == "All"
                        ? subDict.Values.SelectMany(l => l)
                        : subDict.TryGetValue(_currentSubCategory, out var list) ? list : Enumerable.Empty<Sprite>();
                }
            }

            Sprite currentSprite = IsPropertyValid() ? _targetProperty.objectReferenceValue as Sprite : null;
            var spritesList = spritesToDraw.ToList();

            foreach (var sprite in spritesList)
            {
                var capturedSprite = sprite;

                // ── Card ─────────────────────────────────────────────────────────
                int imgSize    = _cardSize - 20;
                int cardHeight = _cardSize + 24;

                var card = new VisualElement();
                card.AddToClassList("picker-card");
                card.style.width  = _cardSize;
                card.style.height = cardHeight;
                card.tooltip      = AssetDatabase.GetAssetPath(sprite);

                if (sprite == currentSprite)
                    card.AddToClassList("picker-card--selected");

                // ── Sprite image ─────────────────────────────────────────────────
                var img = new Image();
                img.image       = AssetPreview.GetAssetPreview(sprite) ?? sprite.texture;
                img.style.width = img.style.height = imgSize;
                img.scaleMode   = ScaleMode.ScaleToFit;

                // ── Name label ───────────────────────────────────────────────────
                var lbl = new Label(sprite.name);
                lbl.tooltip     = sprite.name;
                lbl.style.width = _cardSize - 8;

                card.Add(img);
                card.Add(lbl);

                // ── Click — assign & close ────────────────────────────────────────
                card.RegisterCallback<MouseDownEvent>(_ =>
                {
                    if (IsPropertyValid())
                    {
                        _targetProperty.objectReferenceValue = capturedSprite;
                        _targetProperty.serializedObject.ApplyModifiedProperties();
                    }
                    Close();
                });

                _gridContainer.Add(card);
            }

            if (spritesList.Count == 0)
            {
                var emptyLbl = new Label("No sprites found.");
                emptyLbl.AddToClassList("picker-empty-label");
                _gridContainer.Add(emptyLbl);
            }

            if (_spriteCountLabel != null)
                _spriteCountLabel.text = $"{spritesList.Count} sprite{(spritesList.Count != 1 ? "s" : "")}";
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
            row.AddToClassList("picker-settings-row");

            var lbl = new Label(labelText);
            lbl.AddToClassList("picker-settings-label");
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

        static void SetTextInputBorderColor(TextField field, Color c)
        {
            if (field == null) return;
            var ti = field.Q<VisualElement>("unity-text-input");
            if (ti == null) return;
            ti.style.borderTopColor   = ti.style.borderBottomColor =
            ti.style.borderLeftColor  = ti.style.borderRightColor  = c;
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
}