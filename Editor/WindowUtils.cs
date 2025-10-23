#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Reflection;
using UnityEditor;

namespace trit.timelinebindingresolver{
    public static class WindowUtils
    {
        public static Type FindTypeFromAssemblies(string typeName){
            Type windowType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                Type type = assembly.GetType(typeName);
                if (type != null) {
                    windowType = type;
                    break;
                }
            }
            if (windowType == null) {
                Debug.LogError(string.Format("Cannot get type of {0}.", typeName));
                return null;
            }
            return windowType;
        }

        public static EditorWindow FindWindowFromType(Type windowType, uint index = 0){
            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(windowType);
            if (windows == null || windows.Length == 0)return null;

            EditorWindow timelineWindow = windows[index] as EditorWindow;
            return timelineWindow;
        }

        public static EditorWindow FindWindowFromType(string typeName, uint index = 0){
            var windowType = FindTypeFromAssemblies(typeName);
            return FindWindowFromType(windowType, index);
        }

        public static object GetProperty(object instance, string propertyName, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public){
            PropertyInfo property = instance.GetType().GetProperty(propertyName, flags);
            if (property == null) {
                Debug.LogError(string.Format("Cannot found '{0}' property.", propertyName));
                return null;
            }

            object propertyInstance = property.GetValue(instance);
            if (propertyInstance == null) {
                Debug.LogError(string.Format("Cannot get value of '{0}' property.", propertyName));
                return null;
            }
            return propertyInstance;
        }

        public static void SetProperty(object instance, string propertyName, object value, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public){
            PropertyInfo property = instance.GetType().GetProperty(propertyName, flags);
            if (property == null) {
                Debug.LogError(string.Format("Not found '{0}' property.", propertyName));
                return;
            }
            property.SetValue(instance, value);
        }
    }
}
#endif
