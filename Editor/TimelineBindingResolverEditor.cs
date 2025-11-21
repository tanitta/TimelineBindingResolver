#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace trit.timelinebindingresolver{
    [CustomEditor(typeof(TimelineBindingResolver))]
    class TimelineBindingResolverEditor: Editor{
        SerializedProperty _script;

        // Matching Options
        bool _showFoldoutHeaderOptions = false;
        SerializedProperty _useNameToTrackComparing;
        SerializedProperty _considerTrackGroupForTrackMatching;
        SerializedProperty _useNameToClipComparing;
        SerializedProperty _considerClipName;
        SerializedProperty _considerClipTime;
        SerializedProperty _considerTrackName;
        SerializedProperty _considerTrackGroupForClipMatching;
        SerializedProperty _applyTargetPrefab;
        void OnEnable()
        {
            _script = serializedObject.FindProperty("m_Script");
            _useNameToTrackComparing            = serializedObject.FindProperty("_useNameToTrackComparing");
            _considerTrackGroupForTrackMatching = serializedObject.FindProperty("_considerTrackGroupForTrackMatching");
            _useNameToClipComparing             = serializedObject.FindProperty("_useNameToClipComparing");
            _considerClipName                   = serializedObject.FindProperty("_considerClipName");
            _considerClipTime                   = serializedObject.FindProperty("_considerClipTime");
            _considerTrackName                  = serializedObject.FindProperty("_considerTrackName");
            _considerTrackGroupForClipMatching  = serializedObject.FindProperty("_considerTrackGroupForClipMatching");
            _applyTargetPrefab  = serializedObject.FindProperty("_applyTargetPrefab");
        }

        public override void OnInspectorGUI(){
            var tbr = (TimelineBindingResolver)target;
            if(GUILayout.Button("Collect Bindings And Apply Prefab",GUILayout.Width(240))){
                Undo.RecordObject(tbr, "Collect And Apply Prefab Changes");
                tbr.CollectAndApplyPrefab();
                EditorUtility.SetDirty(tbr);
            };
            if(GUILayout.Button("Collect Bindings",GUILayout.Width(120))){
                Undo.RecordObject(tbr, "Collect Changes");
                tbr.Collect();
                EditorUtility.SetDirty(tbr);
            };
            GUILayout.Space(10);

            OnInspectorGUIOptions();

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }

        public void OnInspectorGUIOptions(){
            _showFoldoutHeaderOptions = EditorGUILayout.BeginFoldoutHeaderGroup(_showFoldoutHeaderOptions, "Options");
            if (_showFoldoutHeaderOptions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_useNameToTrackComparing);
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_considerTrackGroupForTrackMatching);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(_useNameToClipComparing);
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_considerClipName);
                    EditorGUILayout.PropertyField(_considerClipTime);
                    EditorGUILayout.PropertyField(_considerTrackName);
                    EditorGUILayout.PropertyField(_considerTrackGroupForClipMatching);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(_applyTargetPrefab);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
#endif
