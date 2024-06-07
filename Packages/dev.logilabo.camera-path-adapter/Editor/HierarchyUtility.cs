using System;
using System.Collections.Generic;
using UnityEngine;

namespace dev.logilabo.camera_path_adapter.editor
{
    public static class HierarchyUtility
    {
        public static bool IsDescendant(Transform root, Transform target)
        {
            if (root == null || target == null) { return false; }
            if (root == target) { return true; }
            return IsDescendant(root, target.parent);
        }

        public static bool IsDescendant(GameObject root, GameObject target)
        {
            if (root == null || target == null) { return false; }
            return IsDescendant(root.transform, target.transform);
        }


        public static string RelativePath(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                throw new InvalidOperationException("target is not a descendant of root");
            }
            if (root == target) { return ""; }
            var tokens = new List<string>();
            var cur = target;
            while (cur != null && cur != root)
            {
                tokens.Add(cur.name);
                cur = cur.parent;
            }
            if (cur == null)
            {
                throw new InvalidOperationException("target is not a descendant of root");
            }
            tokens.Reverse();
            return string.Join("/", tokens);
        }

        public static string RelativePath(GameObject root, GameObject target)
        {
            if (root == null || target == null)
            {
                throw new InvalidOperationException("target is not a descendant of root");
            }
            return RelativePath(root.transform, target.transform);
        }


        public static Transform PathToObject(Transform root, string path)
        {
            if (path == null) { throw new ArgumentNullException(nameof(path)); }
            if (root == null) { return null; }
            var cur = root;
            foreach (var token in path.Split('/'))
            {
                cur = cur.Find(token);
                if (cur == null) { return null; }
            }
            return cur;
        }

        public static GameObject PathToObject(GameObject root, string path)
        {
            if (path == null) { throw new ArgumentNullException(nameof(path)); }
            if (root == null) { return null; }
            return PathToObject(root.transform, path)?.gameObject;
        }
    }
}