using System.IO;
using BeatThat.GetComponentsExt;
using BeatThat.Placements;
using BeatThat.Pools;
using UnityEditor;
using UnityEngine;

namespace BeatThat.FindResources
{
    public class OnSaveAssetsUpdateResourcePathsByPathPrefix : UnityEditor.AssetModificationProcessor
    {

        static string[] OnWillSaveAssets(string[] pathsSaved)
        {
            ResourcePathsByPathPrefix.InvalidatePaths(pathsSaved);
            return pathsSaved;
        }
    }

    public static class FindResourceByComponentTypeEditorExt
    {
        public static void EditSelectedResource(this FindResourceByComponentType obj, Component owner)
        {
            var asset = obj.GetSelectedAsset();
            if (asset == null)
            {
                return;
            }
            var inst = UnityEditor.PrefabUtility.InstantiatePrefab(asset) as Component;

            inst.transform.SetParent(owner.transform, false);
            inst.name = asset.name;
            PrefabPlacement.OrientToParent(inst.transform, asset.transform);
        }
    }

    [CustomPropertyDrawer(typeof(FindResourceByComponentType))]
    public class FindResourceByComponentTypePropertyDrawer : UnityEditor.PropertyDrawer
    {

        private static readonly Color VALID = Color.cyan;
        private static readonly Color PENDING = Color.yellow;

        override public float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var baseHeight = base.GetPropertyHeight(property, label);
            return Mathf.Max(this.renderedPropertyHeight, baseHeight * 3);
        }

        private int lastValidCount { get; set; }
        private float renderedPropertyHeight { get; set; }

        override public void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var saveColor = GUI.color;

            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            SerializedProperty resourcePathProp = property.FindPropertyRelative("m_resourcePath");
            SerializedProperty selectedComponentTypeProp = property.FindPropertyRelative("m_selectedComponentType");

            var baseHeight = base.GetPropertyHeight(property, label);
            this.renderedPropertyHeight = baseHeight;
            var curRect = new Rect(position.x, position.y, position.width, baseHeight); // position.height);

            GUI.color = this.lastValidCount > 0 ? VALID : PENDING;

            EditorGUI.PropertyField(curRect, resourcePathProp,
                new GUIContent("Resource Path", "a resource path that contains assets each having a component whose type name matches the asset name")
            );
            this.renderedPropertyHeight += baseHeight;

            var resourcePath = resourcePathProp.stringValue ?? "";

            // Don't make child fields be indented
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var selectedComponentTypeName = selectedComponentTypeProp.stringValue ?? "";
            var trimIx = selectedComponentTypeName.LastIndexOf('.');
            if (trimIx > 0)
            {
                selectedComponentTypeName = selectedComponentTypeName.Substring(trimIx + 1);
            }


            using (var prefabs = ListPool<FileInfo>.Get())
            using (var validPrefabs = ListPool<FileInfo>.Get())
            using (var invalidPrefabs = ListPool<FileInfo>.Get())
            {
                ResourcePathsByPathPrefix.FindAllPrefabs(resourcePath, prefabs);

                var oldIx = -1;

                for (var i = 0; i < prefabs.Count; i++)
                {
                    var pName = Path.GetFileNameWithoutExtension(prefabs[i].Name);
                    var pAsset = Resources.Load<GameObject>(string.Format("{0}/{1}", resourcePath, pName));
                    if (pAsset == null)
                    {
                        invalidPrefabs.Add(prefabs[i]);
                        continue;
                    }



                    if (FindComponentWithTypeName(pAsset, pName) != null)
                    {
                        if (pName == selectedComponentTypeName)
                        {
                            oldIx = validPrefabs.Count;
                        }
                        validPrefabs.Add(prefabs[i]);
                    }
                    else
                    {
                        invalidPrefabs.Add(prefabs[i]);
                    }

                }

                this.lastValidCount = validPrefabs.Count;

                using (var prefabNames = ArrayPool<string>.Get(validPrefabs.Count + 1))
                {
                    prefabNames.array[0] = "[none]";
                    for (var nameIx = 0; nameIx < validPrefabs.Count; nameIx++)
                    {
                        prefabNames.array[nameIx + 1] = Path.GetFileNameWithoutExtension(validPrefabs[nameIx].Name);
                    }

                    curRect.y += baseHeight;

                    oldIx += 1; // account for empty first entry

                    GUI.color = validPrefabs.Count > 0 ? VALID : PENDING;

                    var newIx = validPrefabs.Count > 0 ?
                        EditorGUI.Popup(curRect, "Selected Resource Prefab (" + validPrefabs.Count + ")", oldIx, prefabNames.array) :
                        EditorGUI.Popup(curRect, "No Valid Prefabs at Resource Path", oldIx, prefabNames.array);

                    this.renderedPropertyHeight += baseHeight;

                    GUI.color = saveColor;

                    if (newIx != oldIx)
                    {
                        if (newIx <= 0)
                        {
                            selectedComponentTypeProp.stringValue = "";
                        }
                        else
                        {
                            var selectedAssetName = Path.GetFileNameWithoutExtension(validPrefabs[newIx - 1].Name);
                            var selectedAsset = Resources.Load<GameObject>(string.Format("{0}/{1}", resourcePath, selectedAssetName));
                            var c = FindComponentWithTypeName(selectedAsset, selectedAssetName);
                            selectedComponentTypeProp.stringValue = c.GetType().FullName;
                        }
                    }
                }

                if (invalidPrefabs.Count > 0)
                {
                    GUI.color = PENDING;
                    using (var invalidNames = ArrayPool<GUIContent>.Get(invalidPrefabs.Count))
                    {
                        for (var nameIx = 0; nameIx < invalidPrefabs.Count; nameIx++)
                        {
                            invalidNames.array[nameIx] = new GUIContent(Path.GetFileNameWithoutExtension(invalidPrefabs[nameIx].Name));
                        }

                        curRect.y += baseHeight;

                        var selectedIx = EditorGUI.Popup(curRect,
                            new GUIContent("Unusable Prefabs", "These prefabs do not meet the requirement of having a Component whose type name matches the prefab name"),
                            0, invalidNames.array
                        );
                        this.renderedPropertyHeight += baseHeight;
                        curRect.y += curRect.height;

                        var selectedAssetName = Path.GetFileNameWithoutExtension(invalidPrefabs[selectedIx].Name);
                        Debug.LogWarning("path to problem prefab=" + selectedAssetName);
                        var selectedAsset = Resources.Load<GameObject>(string.Format("{0}/{1}", resourcePath, selectedAssetName));
                        Component bestMatch;
                        if (selectedAsset != null && (bestMatch = FindComponentWithNameClosestTo(selectedAsset)) != null)
                        {
                            curRect.height = baseHeight * 6;
                            EditorGUI.HelpBox(curRect, "\nResource prefab " + selectedAssetName
                                + " must have a component with type name " + selectedAssetName
                                + "\n\nFound a component with similar name " + bestMatch.GetType().Name
                                + "\n\nMaybe rename the component class or the prefab to match?\n", MessageType.Warning);
                        }
                        else
                        {
                            curRect.height = baseHeight * 2;
                            EditorGUI.HelpBox(curRect, "Resource prefab " + selectedAssetName + " must have a component with type name " + selectedAssetName, MessageType.Warning);
                        }
                        this.renderedPropertyHeight += curRect.height;

                    }
                    GUI.color = saveColor;
                }
            }

            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }

        private static Component FindComponentWithTypeName(GameObject go, string typeName)
        {
            using (var comps = ListPool<Component>.Get())
            {
                go.GetComponents<Component>(comps, true);
                return comps.Find(c => c.GetType().Name == typeName);
            }
        }

        private Component FindComponentWithNameClosestTo(GameObject go)
        {
            var bestDist = int.MaxValue;
            Component bestComp = null;
            var cname = go.name;
            using (var comps = ListPool<Component>.Get())
            {
                go.GetComponents(comps);
                foreach (var c in comps)
                {
                    var dist = LevenshteinDistance(cname, c.GetType().Name);
                    //Debug.LogWarning("d between " + cname + " and  " + c.GetType().Name + "=" + dist);
                    if (dist < bestDist)
                    {
                        bestComp = c;
                        bestDist = dist;
                    }
                }
            }
            return bestComp;
        }

        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];
            if (n == 0)
            {
                return m;
            }
            if (m == 0)
            {
                return n;
            }
            for (int i = 0; i <= n; d[i, 0] = i++)
                ;
            for (int j = 0; j <= m; d[0, j] = j++)
                ;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Mathf.Min(
                        Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

    }
}



