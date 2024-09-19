#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

using SceneTrackBindingDictionary = System.Collections.Generic.Dictionary<UnityEngine.Object, (string gameObjectPath, string componentTypeName, string assemblyName)>;
using SceneClipBindingDictionary = System.Collections.Generic.Dictionary<UnityEngine.PropertyName, (string gameObjectPath, string componentTypeName, string assemblyName)>;
using UnityEngine.Timeline;

namespace trit
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(PlayableDirector))]
    public class TimelineBindingResolver : MonoBehaviour
    {
        PlayableDirector _director;
        [SerializeField]
        Transform _proxyTransform;
        Dictionary<PlayableOutput, string> _trackToRelPath;

        [SerializeField]
        public List<SceneTrackBinding> _sceneTrackBindings = new List<SceneTrackBinding>();

        [SerializeField]
        public List<SceneClipBinding> _sceneClipBindings = new List<SceneClipBinding>();

        static string _siblingBracketL = "〈";
        static string _siblingBracketR = "〉";

        [ContextMenu("TBR/Apply")]
        public void Apply() {
            var crtPath = GetHierarchyPath(this);
            if (_proxyTransform != null) {
                crtPath = GetHierarchyPath(_proxyTransform);
            }
            ApplyTrackBinding(crtPath);
            ApplyClipBindings(crtPath);
        }

        [ContextMenu("TBR/Collect")]
        public void Collect() {
            var crtPath = GetHierarchyPath(this);
            if (_proxyTransform != null) {
                crtPath = GetHierarchyPath(_proxyTransform);
            }
            CollectTrackBindings(crtPath);
            CollectClipBindings(crtPath);
            DisableTimelinePreview();
        }

        [ContextMenu("TBR/Check")]
        void Check() {
            _director = GetComponent<PlayableDirector>();

            foreach (var binding in _director.playableAsset.outputs)
            {
                var track = binding.sourceObject;
                if(track == null){
                    Debug.LogError("[TBR] Detect none track. Track: " + binding.ToString(), gameObject);
                    continue;
                }
                var o = _director.GetGenericBinding(track);
                if (o is null) {
                        Debug.LogError("[TBR] Detect none track. Track: " + track.name, gameObject);
                    }else{
                        Debug.Log("[TBR] Detect valid track. Track: " + track.name, gameObject);
                }
            }

            foreach (var binding in _director.playableAsset.outputs)
            {
                var trackAsset = binding.sourceObject as TrackAsset;
                foreach (TimelineClip clip in trackAsset.GetClips())
                {
                    foreach (FieldInfo fieldInfo in clip.asset.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (fieldInfo.FieldType.GetField("exposedName") == null) continue;
                        PropertyName exposeName = (PropertyName)fieldInfo.FieldType.GetField("exposedName").GetValue(fieldInfo.GetValue(clip.asset));
                        bool isValid;
                        UnityEngine.Object exposedValue = _director.GetReferenceValue(exposeName, out isValid);
                        if (exposedValue == null || !isValid)
                        {
                            Debug.LogError("[TBR] Detect none clip field. Track: " + trackAsset.name + " / Clip: " + clip.displayName + " / ExposedReference: " + exposeName, gameObject);
                        }else{
                            Debug.Log("[TBR] Detect valid clip field. Track: " + trackAsset.name + " / Clip: " + clip.displayName + " / ExposedReference: " + exposeName, gameObject);
                        }
                    }
                }
            }
        }

        [ContextMenu("TBR/Cleanup/Remove Needless Missing Bindings")]
        public void RemoveNeedlessBindings() {
            var crtPath = GetHierarchyPath(this);
            if (_proxyTransform != null) {
                crtPath = GetHierarchyPath(_proxyTransform);
            }
            var needlessTrackBindings = new List<SceneTrackBinding>();
            foreach (var binding in _sceneTrackBindings){
                if (binding.track == null){
                    needlessTrackBindings.Add(binding);
                    continue;
                }
                var absPath = ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = FindGameObjectFromPath(absPath);
                if(go == null){
                    needlessTrackBindings.Add(binding);
                    continue;
                }
                System.Type componentType = System.Reflection.Assembly.Load("UnityEngine.dll").GetType(binding.componentTypeName);
                if (componentType == typeof(GameObject)) {
                } else {
                    UnityEngine.Component component;
                    if (componentType == null) {
                        component = go.GetComponent(binding.componentTypeName);
                        if(component == null){
                            needlessTrackBindings.Add(binding);
                            continue;
                        }
                    } else {
                        component = go.GetComponent(componentType);
                        if(component == null){
                            needlessTrackBindings.Add(binding);
                            continue;
                        }
                        
                    }
                }
            }
            _sceneTrackBindings = _sceneTrackBindings.Where(b => needlessTrackBindings.FindIndex(n => n.Equals(b)) < 0).ToList();

            var needlessClipBindings = new List<SceneClipBinding>();
            foreach (var binding in _sceneClipBindings){
                bool isValid;
                if (_director.GetReferenceValue(binding.clip, out isValid) != null) continue;
                var absPath = ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = FindGameObjectFromPath(absPath);
                if(go == null){
                    needlessClipBindings.Add(binding);
                    continue;
                }
                System.Type componentType = System.Reflection.Assembly.Load("UnityEngine.dll").GetType(binding.componentTypeName);
                if (componentType == typeof(GameObject)){
                }else{
                    UnityEngine.Component component;
                    if (componentType == null) {
                        component = go.GetComponent(binding.componentTypeName);
                        if(component == null){
                            needlessClipBindings.Add(binding);
                            continue;
                        }
                    } else {
                        component = go.GetComponent(componentType);
                        if(component == null){
                            needlessClipBindings.Add(binding);
                            continue;
                        }
                        
                    }
                }
            }
            _sceneClipBindings = _sceneClipBindings.Where(b => needlessClipBindings.FindIndex(n => n.Equals(b)) < 0).ToList();
        }

        void ApplyTrackBinding(string crtPath) {
            _director = GetComponent<PlayableDirector>();
            foreach (var binding in _sceneTrackBindings)
            {
                if (binding.track == null){
                    Debug.Log("[TBR] Detect needless track binding. Reset and re-collect resolver track list in " + gameObject.name, gameObject);
                    continue;
                }
                // if (_director.GetGenericBinding(binding.track) != null) continue;
                var absPath = ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = FindGameObjectFromPath(absPath);
                if(go == null){
                    Debug.LogWarning("[TBR]Skip applying missing collected binding GameObject.\n" + "Path: " + absPath, _director);
                    continue;
                }
                System.Type componentType = System.Reflection.Assembly.Load("UnityEngine.dll").GetType(binding.componentTypeName);
                if (componentType == typeof(GameObject)) {
                    _director.SetGenericBinding(binding.track, go);
                } else {
                    UnityEngine.Component component;
                    if (componentType == null) {
                        component = go.GetComponent(binding.componentTypeName);
                        if(component == null){
                            Debug.LogWarning("[TBR]Missing collected binding component.\n" + "Path: " + absPath +  "\n" + "Component: " + binding.componentTypeName, gameObject);
                            continue;
                        }
                    } else {
                        component = go.GetComponent(componentType);
                        if(component == null){
                            Debug.LogWarning("[TBR]Missing collected binding component.\n" + "Path: " + absPath +  "\n" + "Component: " + componentType, gameObject);
                            continue;
                        }
                        
                    }
                    _director.SetGenericBinding(binding.track, component);
                }
            }
        }

        void ApplyClipBindings(string crtPath) {
            _director = GetComponent<PlayableDirector>();
            foreach (var binding in _sceneClipBindings){
                // bool isValid;
                // if (_director.GetReferenceValue(binding.clip, out isValid) != null) continue;
                var absPath = ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = FindGameObjectFromPath(absPath);
                if(go == null){
                    Debug.LogWarning("[TBR]Skip applying missing collected binding gameobject. \nPath:" + absPath, _director);
                    continue;
                }
                System.Type componentType = System.Reflection.Assembly.Load("UnityEngine.dll").GetType(binding.componentTypeName);
                if (componentType == typeof(GameObject)){
                    _director.SetReferenceValue (binding.clip, go);
                }else{
                    UnityEngine.Component component;
                    if (componentType == null) {
                        component = go.GetComponent(binding.componentTypeName);
                    } else {
                        component = go.GetComponent(componentType);
                    }
                    _director.SetReferenceValue (binding.clip, component);
                }
            }
        }


        void CollectClipBindings(string crtPath)
        {
            _director = GetComponent<PlayableDirector>();
            var clips = TimelineClips(_director.playableAsset as TimelineAsset);
            var dict = SceneClipBindingsToDict(_sceneClipBindings);
            foreach(var exposeName in PropertyNamesFrom(clips)){
                bool isValid;
                UnityEngine.Object exposedValue = _director.GetReferenceValue(exposeName, out isValid);
                if (exposedValue == null || !isValid)continue;
                var path = GetHierarchyPath(exposedValue);
                var relPath = GetRelativePath(crtPath, path);
                var type = exposedValue.GetType();
                var assemblyName = System.Reflection.Assembly.GetAssembly(type).GetName().Name;
                dict[exposeName] = (relPath.ToString(), type.FullName, assemblyName);
            }
            _sceneClipBindings = SceneClipBindingsDictToArray(dict);
        }

        IEnumerable<PropertyName> PropertyNamesFrom(IEnumerable<TimelineClip> clips){
            foreach (var clip in clips)
            {
                foreach (FieldInfo fieldInfo in clip.asset.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if(fieldInfo.FieldType.GetField("exposedName") == null) continue;
                    PropertyName exposeName = (PropertyName)fieldInfo.FieldType.GetField("exposedName").GetValue(fieldInfo.GetValue(clip.asset));
                    yield return exposeName;
                }
            }
        }

        IEnumerable<TimelineClip> TimelineClips(TimelineAsset timelineAsset)
        {
            foreach (var binding in _director.playableAsset.outputs)
            {
                var trackAsset = binding.sourceObject as TrackAsset;
                if(trackAsset == null) continue;
                foreach (TimelineClip newClip in trackAsset.GetClips())
                {
                    yield return newClip;
                }
            }
        }

        void CollectTrackBindings(string crtPath)
        {
            _director = GetComponent<PlayableDirector>();
            var dict = SceneTrackBindingsToDict(_sceneTrackBindings);
            foreach (var binding in _director.playableAsset.outputs)
            {
                var track = binding.sourceObject;
                var o = _director.GetGenericBinding(track);
                if (o is null) continue;
                var path = GetHierarchyPath(o);
                var relPath = GetRelativePath(crtPath, path);
                if(binding.outputTargetType is null){
                    Debug.LogError("[TBR]Binding outputTargetType is null." + binding.ToString(), gameObject);
                    Debug.Log("from: " + crtPath);
                    Debug.Log("to:   " + path);
                    Debug.Log("rel:  " + relPath);
                    Debug.Log("abs:  " + ConvertRelativePathToAbsolute(relPath, crtPath));
                }
                var assembly = System.Reflection.Assembly.GetAssembly(binding.outputTargetType);
                var assemblyName = assembly.GetName().Name;
                dict[track] = (relPath.ToString(), binding.outputTargetType.FullName, assemblyName);
            }
            _sceneTrackBindings = SceneTrackBindingsDictToArray(dict);

        }

        List<SceneTrackBinding> SceneTrackBindingsDictToArray(in SceneTrackBindingDictionary dict)
        {
            var list = new List<SceneTrackBinding>();
            foreach (var elem in dict)
            {
                var sceneBinding = new SceneTrackBinding();
                sceneBinding.track = elem.Key;
                sceneBinding.gameObjectPath = elem.Value.gameObjectPath;
                sceneBinding.componentTypeName = elem.Value.componentTypeName;
                sceneBinding.assemblyName = elem.Value.assemblyName;
                list.Add(sceneBinding);
            }
            return list;
        }

        List<SceneClipBinding> SceneClipBindingsDictToArray(in SceneClipBindingDictionary dict)
        {
            var list = new List<SceneClipBinding>();
            foreach (var elem in dict)
            {
                var sceneClipBinding = new SceneClipBinding();
                sceneClipBinding.clip = elem.Key;
                sceneClipBinding.gameObjectPath = elem.Value.gameObjectPath;
                sceneClipBinding.componentTypeName = elem.Value.componentTypeName;
                sceneClipBinding.assemblyName = elem.Value.assemblyName;
                list.Add(sceneClipBinding);
            }
            return list;
        }
        SceneTrackBindingDictionary SceneTrackBindingsToDict(in List<SceneTrackBinding> bindings)
        {
            var dict = new Dictionary<UnityEngine.Object, (string gameObjectPath, string componentTypeName, string assemblyName)>();
            foreach (var binding in bindings)
            {
                dict[binding.track] = (binding.gameObjectPath, binding.componentTypeName, binding.assemblyName);
            }
            return dict;
        }
        SceneClipBindingDictionary SceneClipBindingsToDict(in List<SceneClipBinding> bindings)
        {
            var dict = new Dictionary<PropertyName, (string gameObjectPath, string componentTypeName, string assemblyName)>();
            foreach (var binding in bindings)
            {
                dict[binding.clip] = (binding.gameObjectPath, binding.componentTypeName, binding.assemblyName);
            }
            return dict;
        }

        string GetHierarchyPathWithSibling(Transform current)
        {
            string path = current.name + _siblingBracketL + GetSiblingIndexEachSameNames(current) + _siblingBracketR;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + _siblingBracketL + GetSiblingIndexEachSameNames(current) + _siblingBracketR + "/" + path;
            }
            return path;
        }

        void SetRelPath(in PlayableOutput playableOutput, in string relPath)
        {
            if (!_trackToRelPath.ContainsKey(playableOutput)) return;
            _trackToRelPath[playableOutput] = relPath;
        }

        string GetHierarchyPath(UnityEngine.Object o)
        {
            string path = "";
            if (o is UnityEngine.Component) path = GetHierarchyPath(o as UnityEngine.Component);
            if (o is GameObject) path = GetHierarchyPath(o as GameObject);
            return path;
        }

        string GetHierarchyPath(UnityEngine.Component c)
        {
            return GetHierarchyPathWithSibling(c.transform);
        }
        string GetHierarchyPath(GameObject o)
        {
            return GetHierarchyPathWithSibling(o.transform);
        }

        string GetRelativePath(string fromPath, string toPath)
        {

            // string fromPath = GetHierarchyPath(from);
            // string toPath = GetHierarchyPath(to);
            if (fromPath == toPath) return ".";

            // Check if 'to' is a child descendant of 'from'
            if (toPath.StartsWith(fromPath + "/"))
            {
                return toPath.Substring(fromPath.Length + 1);
            }

            // Find common root
            string[] fromSplit = fromPath.Split('/');
            string[] toSplit = toPath.Split('/');
            int index = 0;
            while (index < fromSplit.Length && index < toSplit.Length && fromSplit[index] == toSplit[index])
            {
                index++;
            }

            string relativePath = "";
            // from common root to 'from'
            for (int i = index; i < fromSplit.Length; i++)
            {
                relativePath += "../";
            }
            // from common root to 'to'
            for (int i = index; i < toSplit.Length; i++)
            {
                relativePath += toSplit[i] + (i < toSplit.Length - 1 ? "/" : "");
            }
            return relativePath;
        }

        string GetLongestRelativePath(string fromPath, string toPath)
        {
            var fromSplit = fromPath.Split('/');
            var toSplit = toPath.Split('/');
            int index = 0;

            while (index < fromSplit.Length && index < toSplit.Length && fromSplit[index] == toSplit[index])
            {
                index++;
            }

            string relativePath = "";

            // from から共通のルートまで"../"を追加
            for (int i = index; i < fromSplit.Length; i++)
            {
                relativePath += "../";
            }

            // 共通のルートから to までのパスを追加
            for (int i = index; i < toSplit.Length; i++)
            {
                relativePath += toSplit[i] + (i < toSplit.Length - 1 ? "/" : "");
            }

            return relativePath;
        }

        GameObject FindGameObjectFromPath(string path)
        {
            string[] elements = path.Split('/');

            // Rootから探索を開始
            string rootName = GetNameFromElement(elements[0]);
            int rootIndex = GetSiblingIndexFromElement(elements[0]);
            Transform current = null;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootName == root.name && rootIndex == GetSiblingIndexEachSameNames(root.transform))
                {
                    current = root.transform;
                    break;
                }
            }

            if (current == null) return null; // Rootが見つからなかった場合

            // /で分割して各階層に分ける
            for (int i = 1; i < elements.Length; i++) // 0番目はすでにRootとして処理したので1から
            {
                bool isFound = false;
                foreach (Transform child in current)
                {
                    string expectedName = GetNameFromElement(elements[i]);
                    int expectedIndex = GetSiblingIndexFromElement(elements[i]);
                    if (child.name == expectedName && GetSiblingIndexEachSameNames(child) == expectedIndex)
                    {
                        current = child;
                        isFound = true;
                        break;
                    }
                }

                if (!isFound) return null; // 途中で該当する子が見つからなかった場合
            }

            return current.gameObject;
        }

        string GetNameFromElement(string element)
        {
            int indexStart = element.IndexOf(_siblingBracketL);
            if (indexStart == -1) return element; // _siblingBracketL がない場合
            return element.Substring(0, indexStart);
        }

        int GetSiblingIndexFromElement(string element)
        {
            int indexStart = element.IndexOf(_siblingBracketL);
            int indexEnd = element.IndexOf(_siblingBracketR);
            if (indexStart == -1 || indexEnd == -1) return -1; // _siblingBracketL or _siblingBracketR がない場合
            string indexStr = element.Substring(indexStart + 1, indexEnd - indexStart - 1);
            return int.Parse(indexStr);
        }

        string ConvertRelativePathToAbsolute(string relativePath, string fromPath)
        {
            List<string> baseElements = new List<string>(fromPath.Split('/'));
            string[] relativeElements = relativePath.Split('/');

            foreach (var element in relativeElements)
            {
                if (element == "..")
                {
                    // 一つ上の階層に移動
                    if (baseElements.Count > 0)
                        baseElements.RemoveAt(baseElements.Count - 1);
                }
                else
                {
                    // 下の階層に移動
                    baseElements.Add(element);
                }
            }
            return string.Join("/", baseElements);
        }

        int GetSiblingIndexEachSameNames(in Transform target)
        {
            List<Transform> children;
            if (target.parent == null)
            {
                children = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(o => o.transform).ToList();
            }
            else
            {
                children = target.parent.Cast<Transform>().ToList();
            }
            var targetName = target.name;
            var targetHash = target.GetHashCode();
            return children.Where(c => c.name == targetName).ToList().FindIndex(t => t.GetHashCode() == targetHash);

            // High cost implementation
            // var path = GetHierarchyPath(target);
            // var sameNames = Resources.FindObjectsOfTypeAll<Transform>().Where(t => GetHierarchyPath(t) == path).ToList();
            // var hash = target.GetHashCode();
            // return sameNames.FindIndex(t => t.GetHashCode() == hash);
        }

        void DisableTimelinePreview() {
            Type timelineWindowType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies) {
                Type type = assembly.GetType("UnityEditor.Timeline.TimelineWindow");
                if (type != null) {
                    timelineWindowType = type;
                    // Debug.Log("Get TimelineWindow Type.");
                    break;
                }
            }

            if (timelineWindowType == null) {
                Debug.LogError("Cannot get type of TimelineWindow.");
                return;
            }

            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(timelineWindowType);
            if (windows == null || windows.Length == 0)return;

            EditorWindow timelineWindow = windows[0] as EditorWindow;
            if (timelineWindow == null) {
                Debug.LogError("Timeline window is not active.");
                return;
            }

            PropertyInfo stateProperty = timelineWindowType.GetProperty("state", BindingFlags.Instance | BindingFlags.Public);
            if (stateProperty == null) {
                Debug.LogError("Cannot found 'state' property.");
                return;
            }

            object stateInstance = stateProperty.GetValue(timelineWindow);
            if (stateInstance == null) {
                Debug.LogError("Cannot get value of 'state' property.");
                return;
            }

            PropertyInfo previewModeProperty = stateInstance.GetType().GetProperty("previewMode", BindingFlags.Instance | BindingFlags.Public);
            if (previewModeProperty == null) {
                Debug.LogError("Not found 'previewMode' property.");
                return;
            }

            previewModeProperty.SetValue(stateInstance, false);
        }
    }

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

    [System.Serializable]
    public struct SceneTrackBinding
    {
        public UnityEngine.Object track;
        public string gameObjectPath;
        public string componentTypeName;
        public string assemblyName;
    }

    [System.Serializable]
    public struct SceneClipBinding
    {
        public UnityEngine.PropertyName clip;
        public string gameObjectPath;
        public string componentTypeName;
        public string assemblyName;
    }

    // https://docs.unity3d.com/ja/2019.4/Manual/RunningEditorCodeOnLaunch.html
    [InitializeOnLoad]
    public class ApplyTBROnSceneLoaded{
        private static readonly string TMP_TBR_RUNNING_LOCKFILE = "Temp/TBRRunningLockfile";

        static ApplyTBROnSceneLoaded(){
            // [Unityエディタでプロジェクトを\(初回\)起動した時の判定【Unity】【エディタ拡張】 \- \(:3\[kanのメモ帳\]](https://kan-kikuchi.hatenablog.com/entry/Editor_Startup_Confirmer)
            bool shouldRegisteredEventOnStartupOnly = false; // For Debug
            if (shouldRegisteredEventOnStartupOnly){
                bool onStartup = !File.Exists(TMP_TBR_RUNNING_LOCKFILE);
                if (!onStartup && shouldRegisteredEventOnStartupOnly) return;
                File.Create(TMP_TBR_RUNNING_LOCKFILE);
            }

            // [Unity \- Scripting API: SceneManagement\.EditorSceneManager\.sceneOpened](https://docs.unity3d.com/ScriptReference/SceneManagement.EditorSceneManager-sceneOpened.html)
            EditorSceneManager.sceneOpened += ApplyTBROnSceneLoadedCallback;
            // [Bug \- EditorSceneManager\.sceneOpened not called on Editor startup\. \- Unity Forum](https://forum.unity.com/threads/editorscenemanager-sceneopened-not-called-on-editor-startup.1259672/)
            EditorApplication.delayCall += ApplyTBRDellayCall;
            Debug.Log("[TBR] Applied all bindings on scene loaded.");
        }

        static void ApplyTBRDellayCall(){
            ApplyTBROnSceneLoadedCallback(EditorSceneManager.GetActiveScene(),OpenSceneMode.Single);
            EditorApplication.delayCall -= ApplyTBRDellayCall; // Call on startup scene loading only. ignore re-calling on script compiled.
            Debug.Log("[TBR] Call Dellay");
        }

        static void ApplyTBROnSceneLoadedCallback(Scene scene, OpenSceneMode mode){
            var roots = scene.GetRootGameObjects();
            foreach(var root in roots){
                var resolvers = root.GetComponentsInChildren<TimelineBindingResolver>(true);
                foreach(var resolver in resolvers){
                    resolver.Apply();
                }
            }
        }
    }
}
#endif
