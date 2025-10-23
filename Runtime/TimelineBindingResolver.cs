#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine.Timeline;
using System.Text.RegularExpressions;

namespace trit.timelinebindingresolver
{
    using SceneTrackBindingDictionary = System.Collections.Generic.Dictionary<UnityEngine.Object, (string name, string gameObjectPath, string componentTypeName, string assemblyName)>;
    using SceneClipBindingDictionary = System.Collections.Generic.Dictionary<UnityEngine.PropertyName, (string name, string gameObjectPath, string componentTypeName, string assemblyName)>;

    [ExecuteInEditMode]
    [RequireComponent(typeof(PlayableDirector))]
    public class TimelineBindingResolver : MonoBehaviour
    {
        PlayableDirector _director;
        [SerializeField]
        Transform _proxyTransform;
        Dictionary<PlayableOutput, string> _trackToRelPath;

        [SerializeField]
        public bool _useNameToTrackComparing = false;
        [SerializeField]
        public bool _useNameToClipComparing = false;
        [SerializeField]
        public bool _considerClipName = true;
        [SerializeField]
        public bool _considerClipTime = false;
        [SerializeField]
        public bool _considerTrackName = true;

        [SerializeField]
        public List<SceneTrackBinding> _sceneTrackBindings = new List<SceneTrackBinding>();

        [SerializeField]
        public List<SceneClipBinding> _sceneClipBindings = new List<SceneClipBinding>();

        [ContextMenu("TBR/Apply")]
        public void Apply() {
            var crtPath = SceneUtils.GetHierarchyPath(this);
            if (_proxyTransform != null) {
                crtPath = SceneUtils.GetHierarchyPath(_proxyTransform);
            }
            ApplyTrackBinding(crtPath);
            ApplyClipBindings(crtPath);
        }

        [ContextMenu("TBR/Collect")]
        public void Collect() {
            var crtPath = SceneUtils.GetHierarchyPath(this);
            if (_proxyTransform != null) {
                crtPath = SceneUtils.GetHierarchyPath(_proxyTransform);
            }
            CollectTrackBindings(crtPath);
            CollectClipBindings(crtPath);
            DisableTimelinePreview();
        }

        void DisableTimelinePreview() {
            EditorWindow timelineWindow = WindowUtils.FindWindowFromType("UnityEditor.Timeline.TimelineWindow");
            if (timelineWindow == null) {
                Debug.LogError("Timeline window is not active.");
                return;
            }
            var stateInstance = WindowUtils.GetProperty(timelineWindow, "state");
            WindowUtils.SetProperty(stateInstance, "previewMode", false);
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
                    foreach (var exposedParm in PropertyNamesFrom(clip)){
                        bool isValid;
                        UnityEngine.Object exposedValue = _director.GetReferenceValue(exposedParm.exposedName, out isValid);
                        if (exposedValue == null || !isValid)
                        {
                            Debug.LogError("[TBR] Detect none clip field. Track: " + trackAsset.name + " / Clip: " + clip.displayName + " / ExposedReference: " + exposedParm.exposedName, gameObject);
                        }else{
                            Debug.Log("[TBR] Detect valid clip field. Track: " + trackAsset.name + " / Clip: " + clip.displayName + " / ExposedReference: " + exposedParm.exposedName, gameObject);
                        }
                    }
                }
            }
        }

        [ContextMenu("TBR/Cleanup/Remove Needless Missing Bindings")]
        public void RemoveNeedlessBindings() {
            var crtPath = SceneUtils.GetHierarchyPath(this);
            if (_proxyTransform != null) {
                crtPath = SceneUtils.GetHierarchyPath(_proxyTransform);
            }

            // Check tracks
            var needlessTrackBindings = new List<SceneTrackBinding>();
            foreach (var binding in _sceneTrackBindings){
                if (Empty(binding)){
                    needlessTrackBindings.Add(binding);
                    continue;
                }
                var absPath = SceneUtils.ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = SceneUtils.FindGameObjectFromPath(absPath);
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

            // Check clips
            var needlessClipBindings = new List<SceneClipBinding>();
            foreach (var binding in _sceneClipBindings){
                bool isValid;
                if (GetReferenceValueFromClip(_director, binding, out isValid) != null) continue;
                var absPath = SceneUtils.ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = SceneUtils.FindGameObjectFromPath(absPath);
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

        Object GetReferenceValueFromClip(PlayableDirector director, SceneClipBinding clip, out bool isValid){
            var queryPropertyName = clip.clip;
            if(_useNameToClipComparing){
                // Override queryClip if needed
                // Warning: Very naive implementation
                var exposedParms = new List<ExposedParm>();
                foreach (var binding in director.playableAsset.outputs)
                {
                    var trackAsset = binding.sourceObject as TrackAsset;

                    foreach (TimelineClip c in trackAsset.GetClips())
                    {
                        foreach (var exposedParm in PropertyNamesFrom(c)){
                            exposedParms.Add(exposedParm);
                        }
                    }
                }
                var matchings = exposedParms.Where(n => CompareClipName(n.name, clip.clipName));
                if(!matchings.Any()){
                    Debug.LogWarning("[TBR]No matching clip name: " + clip.clipName);
                    isValid = false;
                    return null;
                }
                // Check duplication
                if(1<matchings.Count()){
                    Debug.LogWarning("[TBR]Detected duplicate clips: " + clip.clipName + "\n"+"Please ensure all clips have unique identifiers.");
                    isValid = false;
                    return null;
                }
                queryPropertyName = matchings.First().exposedName;
            }
            return director.GetReferenceValue(queryPropertyName, out isValid);
        }

        void SetReferenceValueFromClip(PlayableDirector director, SceneClipBinding clip, Object value){
            var queryPropertyName = clip.clip;
            if(_useNameToClipComparing){
                // Override queryClip if needed
                // Warning: Very naive implementation
                var exposedParms = new List<ExposedParm>();
                foreach (var binding in director.playableAsset.outputs)
                {
                    var trackAsset = binding.sourceObject as TrackAsset;

                    foreach (TimelineClip c in trackAsset.GetClips())
                    {
                        foreach (var exposedParm in PropertyNamesFrom(c)){
                            exposedParms.Add(exposedParm);
                        }
                    }
                }
                var matchings = exposedParms.Where(n => CompareClipName(n.name, clip.clipName));
                if(!matchings.Any()){
                    Debug.LogWarning("[TBR]No matching clip name: " + clip.clipName);
                    return;
                }
                // Check duplication
                if(1<matchings.Count()){
                    Debug.LogWarning("[TBR]Detected duplicate clips: " + clip.clipName + "\n"+"Please ensure all clips have unique identifiers.");
                    return;
                }
                queryPropertyName = matchings.First().exposedName;
            }
            director.SetReferenceValue (queryPropertyName, value);
        }

        bool CompareClipName(string lhs, string rhs){
            return RebuildClipString(lhs, _considerClipName, _considerClipTime, _considerTrackName) == RebuildClipString(rhs, _considerClipName, _considerClipTime, _considerTrackName);
        }

        static string RebuildClipString(string input, bool useClipName, bool useClipTime, bool useTrackName)
        {
            var regex = new Regex(@"^(.*?)\s*\(([\d\.]+),\s*([\d\.]+)\):([\d\.]+)\s*\|\s*(.*)$");
            var match = regex.Match(input);

            if (!match.Success)
            {
                Debug.LogWarning("Not match pattern.");
                return input;
            }

            string clipName = match.Groups[1].Value.Trim();
            string start = match.Groups[2].Value;
            string end = match.Groups[3].Value;
            string offset = match.Groups[4].Value;
            string trackName = match.Groups[5].Value.Trim();

            string result = "";
            bool hasPrev = false;

            if (useClipName)
            {
                result += clipName;
                hasPrev = true;
            }

            if (useClipTime)
            {
                if (hasPrev) result += " ";
                result += $"({start}, {end}):{offset}";
                hasPrev = true;
            }

            if (useTrackName)
            {
                if (hasPrev) result += " | ";
                result += trackName;
            }

            return result;
        }

        void ApplyTrackBinding(string crtPath) {
            _director = GetComponent<PlayableDirector>();
            foreach (var binding in _sceneTrackBindings)
            {
                if (Empty(binding)){
                    Debug.Log("[TBR] Detect needless track binding. Reset and re-collect resolver track list in " + gameObject.name, gameObject);
                    continue;
                }
                var absPath = SceneUtils.ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = SceneUtils.FindGameObjectFromPath(absPath);
                if(go == null){
                    Debug.LogWarning("[TBR]Skip applying missing collected binding GameObject.\n" + "Path: " + absPath, _director);
                    continue;
                }
                System.Type componentType = System.Reflection.Assembly.Load("UnityEngine.dll").GetType(binding.componentTypeName);
                if (componentType == typeof(GameObject)) {
                    SetGenericBindingFromTrack(_director, binding, go);
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
                    SetGenericBindingFromTrack(_director, binding, component);
                }
            }
        }

        void SetGenericBindingFromTrack(PlayableDirector director, SceneTrackBinding track, Object value){
            var targetTrackBinding = track.track as Object;
            if(_useNameToTrackComparing){
                var matchings = _director.playableAsset.outputs.Where(b => b.streamName == track.trackName);
                if(!matchings.Any()){
                    Debug.LogWarning("[TBR]No matching track name: " + track.trackName);
                    return;
                }

                // Check duplication
                if(1<matchings.Count()){
                    Debug.LogWarning("[TBR]Detected duplicate tracks: " + track.trackName + "\n"+"Please ensure all clips have unique identifiers.");
                    return;
                }

                targetTrackBinding = matchings.First().sourceObject;
            }
            director.SetGenericBinding(targetTrackBinding, value);
        }

        void ApplyClipBindings(string crtPath) {
            _director = GetComponent<PlayableDirector>();
            foreach (var binding in _sceneClipBindings){
                // bool isValid;
                // if (_director.GetReferenceValue(binding.clip, out isValid) != null) continue;
                var absPath = SceneUtils.ConvertRelativePathToAbsolute(binding.gameObjectPath, crtPath);
                var go = SceneUtils.FindGameObjectFromPath(absPath);
                if(go == null){
                    Debug.LogWarning("[TBR]Skip applying missing collected binding gameobject. \nPath:" + absPath, _director);
                    continue;
                }
                System.Type componentType = System.Reflection.Assembly.Load("UnityEngine.dll").GetType(binding.componentTypeName);
                if (componentType == typeof(GameObject)){
                    SetReferenceValueFromClip(_director, binding, go);
                }else{
                    UnityEngine.Component component;
                    if (componentType == null) {
                        component = go.GetComponent(binding.componentTypeName);
                    } else {
                        component = go.GetComponent(componentType);
                    }
                    SetReferenceValueFromClip(_director, binding, component);
                }
            }
        }

        bool Empty(in SceneTrackBinding track){
            if(_useNameToTrackComparing){
                return !track.trackName.Any();
            }else{
                return track.track == null;
            }
        }

        bool Empty(in SceneClipBinding clip){
            if(_useNameToTrackComparing){
                return !clip.clipName.Any();
            }else{
                return clip.clip == null;
            }
        }

        void CollectClipBindings(string crtPath)
        {
            _director = GetComponent<PlayableDirector>();
            var clips = TimelineClips(_director.playableAsset as TimelineAsset);
            var dict = SceneClipBindingsToDict(_sceneClipBindings);
            foreach(var exposedParm in PropertyNamesFrom(clips)){
                bool isValid;
                UnityEngine.Object exposedValue = _director.GetReferenceValue(exposedParm.exposedName, out isValid);
                if (exposedValue == null || !isValid)continue;
                var path = SceneUtils.GetHierarchyPath(exposedValue);
                var relPath = SceneUtils.GetRelativePath(crtPath, path);
                var type = exposedValue.GetType();
                var assemblyName = System.Reflection.Assembly.GetAssembly(type).GetName().Name;
                dict[exposedParm.exposedName] = (exposedParm.name, relPath.ToString(), type.FullName, assemblyName);
            }
            _sceneClipBindings = SceneClipBindingsDictToArray(dict);
        }

        struct ExposedParm{
            public PropertyName exposedName;
            public string name;
        }

        static IEnumerable<ExposedParm> PropertyNamesFrom(TimelineClip clip){
            var query = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (FieldInfo fieldInfo in clip.asset.GetType().GetFields(query))
            {
                if(fieldInfo.FieldType.GetField("exposedName") == null) continue;
                PropertyName exposedName = (PropertyName)fieldInfo.FieldType.GetField("exposedName").GetValue(fieldInfo.GetValue(clip.asset));
                var result = new ExposedParm();
                result.name = clip.ToString() + "." + fieldInfo.Name;
                result.exposedName = exposedName;
                yield return result;
            }
        }

        static IEnumerable<ExposedParm> PropertyNamesFrom(IEnumerable<TimelineClip> clips){
            foreach (var clip in clips)
            {
                foreach (var exposedParm in PropertyNamesFrom(clip)){
                    yield return exposedParm;
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
                var path = SceneUtils.GetHierarchyPath(o);
                var relPath = SceneUtils.GetRelativePath(crtPath, path);
                if(binding.outputTargetType is null){
                    Debug.LogError("[TBR]Binding outputTargetType is null." + binding.ToString(), gameObject);
                    Debug.Log("from: " + crtPath);
                    Debug.Log("to:   " + path);
                    Debug.Log("rel:  " + relPath);
                    Debug.Log("abs:  " + SceneUtils.ConvertRelativePathToAbsolute(relPath, crtPath));
                }
                var assembly = System.Reflection.Assembly.GetAssembly(binding.outputTargetType);
                var assemblyName = assembly.GetName().Name;
                dict[track] = (track.name, relPath.ToString(), binding.outputTargetType.FullName, assemblyName);
            }
            _sceneTrackBindings = SceneTrackBindingsDictToArray(dict);

        }

        static List<SceneTrackBinding> SceneTrackBindingsDictToArray(in SceneTrackBindingDictionary dict)
        {
            var list = new List<SceneTrackBinding>();
            foreach (var elem in dict)
            {
                var sceneBinding = new SceneTrackBinding();
                sceneBinding.track = elem.Key;
                sceneBinding.trackName = elem.Value.name;
                sceneBinding.gameObjectPath = elem.Value.gameObjectPath;
                sceneBinding.componentTypeName = elem.Value.componentTypeName;
                sceneBinding.assemblyName = elem.Value.assemblyName;
                list.Add(sceneBinding);
            }
            return list;
        }

        static List<SceneClipBinding> SceneClipBindingsDictToArray(in SceneClipBindingDictionary dict)
        {
            var list = new List<SceneClipBinding>();
            foreach (var elem in dict)
            {
                var sceneClipBinding = new SceneClipBinding();
                sceneClipBinding.clip = elem.Key;
                sceneClipBinding.clipName = elem.Value.name;
                sceneClipBinding.gameObjectPath = elem.Value.gameObjectPath;
                sceneClipBinding.componentTypeName = elem.Value.componentTypeName;
                sceneClipBinding.assemblyName = elem.Value.assemblyName;
                list.Add(sceneClipBinding);
            }
            return list;
        }
        static SceneTrackBindingDictionary SceneTrackBindingsToDict(in List<SceneTrackBinding> bindings)
        {
            var dict = new SceneTrackBindingDictionary();
            foreach (var binding in bindings)
            {
                dict[binding.track] = (
                    binding.trackName,
                    binding.gameObjectPath,
                    binding.componentTypeName,
                    binding.assemblyName
                );
            }
            return dict;
        }
        static SceneClipBindingDictionary SceneClipBindingsToDict(in List<SceneClipBinding> bindings)
        {
            var dict = new SceneClipBindingDictionary();
            foreach (var binding in bindings)
            {
                dict[binding.clip] = (
                    binding.clipName,
                    binding.gameObjectPath,
                    binding.componentTypeName,
                    binding.assemblyName
                );
            }
            return dict;
        }
    }

    [System.Serializable]
    public struct SceneTrackBinding
    {
        public UnityEngine.Object track;
        public string trackName;
        public string gameObjectPath;
        public string componentTypeName;
        public string assemblyName;
    }

    [System.Serializable]
    public struct SceneClipBinding
    {
        public UnityEngine.PropertyName clip;
        public string clipName;
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
