using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class BiomeDrawer : OdinValueDrawer<Biome>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        var biome = ValueEntry.SmartValue;
        var text = biome != null ? biome.name : "<None>";
        EditorGUILayout.LabelField(label, new GUIContent(text));
    }
}