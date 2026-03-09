using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(CategorizedSpriteAttribute))]
public class CategorizedSpriteDrawer : PropertyDrawer
{
    // ── Palette ───────────────────────────────────────────────────────────────
    static readonly Color ColFieldBg     = new Color(0.12f, 0.12f, 0.12f);
    static readonly Color ColBorder      = new Color(0.24f, 0.24f, 0.24f);
    static readonly Color ColBorderHov   = new Color(0.40f, 0.40f, 0.40f);
    static readonly Color ColLabel       = new Color(0.70f, 0.70f, 0.70f);
    static readonly Color ColAmber       = new Color(0.78f, 0.55f, 0.16f);
    static readonly Color ColAmberDim    = new Color(0.63f, 0.43f, 0.16f);
    static readonly Color ColAmberChipBg = new Color(0.18f, 0.14f, 0.07f);

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        // ── Root row ─────────────────────────────────────────────────────────
        var root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems    = Align.Center;
        root.style.marginTop     = 2;
        root.style.marginBottom  = 2;
        root.style.height        = 24;

        // ── Field label ──────────────────────────────────────────────────────
        var lbl = new Label(property.displayName);
        lbl.style.width          = Mathf.Max(90f, EditorGUIUtility.labelWidth - 4f);
        lbl.style.minWidth       = 90;
        lbl.style.color          = ColLabel;
        lbl.style.fontSize       = 12;
        lbl.style.paddingRight   = 4;
        lbl.style.flexShrink     = 0;
        lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
        root.Add(lbl);

        // ── Fields wrapper ───────────────────────────────────────────────────
        var wrap = new VisualElement();
        wrap.style.flexDirection           = FlexDirection.Row;
        wrap.style.alignItems              = Align.Center;
        wrap.style.flexGrow                = 1;
        wrap.style.backgroundColor         = ColFieldBg;
        wrap.style.borderTopLeftRadius     = 4;
        wrap.style.borderTopRightRadius    = 4;
        wrap.style.borderBottomLeftRadius  = 4;
        wrap.style.borderBottomRightRadius = 4;
        wrap.style.borderTopWidth          = 1;
        wrap.style.borderBottomWidth       = 1;
        wrap.style.borderLeftWidth         = 1;
        wrap.style.borderRightWidth        = 1;
        wrap.style.borderTopColor          = ColBorder;
        wrap.style.borderBottomColor       = ColBorder;
        wrap.style.borderLeftColor         = ColBorder;
        wrap.style.borderRightColor        = ColBorder;
        wrap.style.paddingLeft             = 4;
        wrap.style.paddingRight            = 2;
        wrap.style.height                  = 24;
        wrap.style.overflow                = Overflow.Hidden;

        wrap.RegisterCallback<MouseEnterEvent>(_ => SetBorderColor(wrap, ColBorderHov));
        wrap.RegisterCallback<MouseLeaveEvent>(_ => SetBorderColor(wrap, ColBorder));

        // ── Object field ─────────────────────────────────────────────────────
        var objField = new ObjectField { bindingPath = property.propertyPath };
        objField.objectType    = typeof(Sprite);
        objField.label         = "";
        objField.style.flexGrow   = 1;
        objField.style.marginTop  = 0;
        objField.style.marginBottom = 0;
        objField.style.height     = 22;

        objField.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            var ti = objField.Q<VisualElement>("unity-object-field-display");
            if (ti != null)
            {
                ti.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                ti.style.borderTopWidth  = ti.style.borderBottomWidth =
                ti.style.borderLeftWidth = ti.style.borderRightWidth  = 0;
            }
            var inner = objField.Q<Label>();
            if (inner != null) inner.style.color = ColLabel;
        });

        wrap.Add(objField);

        // ── "Pick" button ────────────────────────────────────────────────────
        var pickBtn = new Button();
        pickBtn.text = "Pick";
        pickBtn.style.backgroundColor         = ColAmberChipBg;
        pickBtn.style.color                   = ColAmber;
        pickBtn.style.fontSize                = 10;
        pickBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        pickBtn.style.borderTopLeftRadius     = 3;
        pickBtn.style.borderTopRightRadius    = 3;
        pickBtn.style.borderBottomLeftRadius  = 3;
        pickBtn.style.borderBottomRightRadius = 3;
        pickBtn.style.borderTopWidth          = 1;
        pickBtn.style.borderBottomWidth       = 1;
        pickBtn.style.borderLeftWidth         = 1;
        pickBtn.style.borderRightWidth        = 1;
        pickBtn.style.borderTopColor          = ColAmberDim;
        pickBtn.style.borderBottomColor       = ColAmberDim;
        pickBtn.style.borderLeftColor         = ColAmberDim;
        pickBtn.style.borderRightColor        = ColAmberDim;
        pickBtn.style.paddingLeft             = 8;
        pickBtn.style.paddingRight            = 8;
        pickBtn.style.marginLeft              = 4;
        pickBtn.style.marginRight             = 2;
        pickBtn.style.height                  = 18;
        pickBtn.style.flexShrink              = 0;
        pickBtn.clicked += () => CategorizedSpritePicker.ShowWindow(property);

        wrap.Add(pickBtn);
        root.Add(wrap);

        return root;
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var fieldRect  = new Rect(position.x, position.y, position.width - 60, position.height);
        var buttonRect = new Rect(position.x + position.width - 55, position.y, 55, position.height);

        EditorGUI.PropertyField(fieldRect, property, label);

        if (GUI.Button(buttonRect, "Pick"))
            CategorizedSpritePicker.ShowWindow(property);

        EditorGUI.EndProperty();
    }

    static void SetBorderColor(VisualElement el, Color c)
    {
        el.style.borderTopColor    = c;
        el.style.borderBottomColor = c;
        el.style.borderLeftColor   = c;
        el.style.borderRightColor  = c;
    }
}