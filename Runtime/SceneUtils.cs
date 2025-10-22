using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace trit.timelinebindingresolver
{
    public static class SceneUtils{
        static string _siblingBracketL = "〈";
        static string _siblingBracketR = "〉";

        static string GetHierarchyPathWithSibling(Transform current)
        {
            string path = current.name + _siblingBracketL + GetSiblingIndexEachSameNames(current) + _siblingBracketR;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + _siblingBracketL + GetSiblingIndexEachSameNames(current) + _siblingBracketR + "/" + path;
            }
            return path;
        }

        public static string GetHierarchyPath(UnityEngine.Object o)
        {
            string path = "";
            if (o is UnityEngine.Component) path = GetHierarchyPath(o as UnityEngine.Component);
            if (o is GameObject) path = GetHierarchyPath(o as GameObject);
            return path;
        }

        public static string ConvertRelativePathToAbsolute(string relativePath, string fromPath)
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

        public static string GetRelativePath(string fromPath, string toPath)
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

        public static GameObject FindGameObjectFromPath(string path)
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

        static string GetHierarchyPath(UnityEngine.Component c)
        {
            return GetHierarchyPathWithSibling(c.transform);
        }

        static string GetHierarchyPath(GameObject o)
        {
            return GetHierarchyPathWithSibling(o.transform);
        }

        static string GetLongestRelativePath(string fromPath, string toPath)
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


        static string GetNameFromElement(string element)
        {
            int indexStart = element.IndexOf(_siblingBracketL);
            if (indexStart == -1) return element; // _siblingBracketL がない場合
            return element.Substring(0, indexStart);
        }

        static int GetSiblingIndexFromElement(string element)
        {
            int indexStart = element.IndexOf(_siblingBracketL);
            int indexEnd = element.IndexOf(_siblingBracketR);
            if (indexStart == -1 || indexEnd == -1) return -1; // _siblingBracketL or _siblingBracketR がない場合
            string indexStr = element.Substring(indexStart + 1, indexEnd - indexStart - 1);
            return int.Parse(indexStr);
        }


        static int GetSiblingIndexEachSameNames(in Transform target)
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
    }
}
