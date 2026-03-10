using RookieDev0.CategorizedPicker;
using UnityEngine;
namespace RookieDev0.CategorizedSpritePicker.Demo
{
    public class CategorizedSpritePickerDEMO : MonoBehaviour
    {
        [Categorized] public Sprite SpriteA;
        [Categorized, SerializeField] private Sprite SpriteB;
        [field: Categorized, SerializeField] public Sprite SpriteC { get; private set; }
        [Categorized] public GameObject PrefabA;

    }
}
