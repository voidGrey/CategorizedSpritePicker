using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CategorizedSpriteAttribute))]
public class CategorizedSpriteDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Calculate rects for the object field and the button
        Rect fieldRect = new Rect(position.x, position.y, position.width - 60, position.height);
        Rect buttonRect = new Rect(position.x + position.width - 55, position.y, 55, position.height);

        // Draw the standard Sprite object field
        EditorGUI.PropertyField(fieldRect, property, label);

        // Draw the "Pick" button
        if (GUI.Button(buttonRect, "Pick"))
        {
            // Open your custom window and pass the property to it
            CategorizedSpritePicker.ShowWindow(property);
        }

        EditorGUI.EndProperty();
    }
}