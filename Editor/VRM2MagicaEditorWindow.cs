using System;
using UnityEditor;
using UnityEngine;
using UniVRM10;
using VRM2Magica.Runtime;

namespace VRM2Magica.Editor.Editor
{
    public class Vrm2MagicaEditorWindow : EditorWindow
    {
        [MenuItem("Tools/VRM2Magica")]
        private static void Init()
        {
            var w =  GetWindow<Vrm2MagicaEditorWindow>();
            w.Show();
        }

        [SerializeField] private Vrm10Instance? vrm10Instance;

        private void OnGUI()
        {
            vrm10Instance = EditorGUILayout.ObjectField(
                "Target",
                vrm10Instance,
                typeof(Vrm10Instance),
                allowSceneObjects: true
            ) as Vrm10Instance;

            
            EditorGUI.BeginDisabledGroup(vrm10Instance == null);
            if (GUILayout.Button("Convert"))
            {
                RunConvert();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.LabelField("Pressing the Convert button generates the converted GameObject on the scene.");
        }

        private void RunConvert()
        {
            if (vrm10Instance == null)
            {
                return;
            }

            var sourceGameObject = vrm10Instance.gameObject;
            var destGameObject = Instantiate(sourceGameObject);
            destGameObject.name = $"{sourceGameObject.name}_Magica";
            Vrm10ToMagicaConverter.Convert(destGameObject.GetComponent<Vrm10Instance>());
        }
    }
}