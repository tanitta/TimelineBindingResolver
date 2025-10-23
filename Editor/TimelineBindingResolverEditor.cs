#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace trit.timelinebindingresolver{
    [CustomEditor(typeof(TimelineBindingResolver))]
    class TimelineBindingResolverEditor: Editor{
        public override void OnInspectorGUI(){
            var tbr = (TimelineBindingResolver)target;
            if(GUILayout.Button("Collect Bindings",GUILayout.Width(120))){
                Undo.RecordObject(tbr, "Collect Changes");
                tbr.Collect();
                EditorUtility.SetDirty(tbr);
                // serializedObject.Update();
            };
            GUILayout.Space(10);
            DrawDefaultInspector();
        }
    }
}
#endif
