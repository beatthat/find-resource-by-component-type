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
			if (asset == null) {
				return;
			}
			var inst = UnityEditor.PrefabUtility.InstantiatePrefab (asset) as Component;

			inst.transform.SetParent (owner.transform, false);
			inst.name = asset.name;
			PrefabPlacement.OrientToParent (inst.transform, asset.transform);
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
			var height = baseHeight * 3;

			return height;
		}

		private int lastValidCount { get; set; }

		override public void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var saveColor = GUI.color;
			
			EditorGUI.BeginProperty(position, label, property);

			// Draw label
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			SerializedProperty resourcePathProp = property.FindPropertyRelative("m_resourcePath");
			SerializedProperty selectedComponentTypeProp = property.FindPropertyRelative("m_selectedComponentType");

			var baseHeight = base.GetPropertyHeight(property, label);
			var curRect = new Rect(position.x, position.y, position.width, baseHeight); // position.height);

			GUI.color = this.lastValidCount > 0 ? VALID : PENDING;

			EditorGUI.PropertyField (curRect, resourcePathProp,
				new GUIContent("Resource Path", "a resource path that contains assets each having a component whose type name matches the asset name")
			);

			var resourcePath = resourcePathProp.stringValue ?? "";

			// Don't make child fields be indented
			int indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			var selectedComponentTypeName = selectedComponentTypeProp.stringValue ?? "";
			var trimIx = selectedComponentTypeName.LastIndexOf ('.');
			if(trimIx > 0) {
				selectedComponentTypeName = selectedComponentTypeName.Substring (trimIx + 1);
			}


			using (var prefabs  = ListPool<FileInfo>.Get ()) 
			using (var validPrefabs = ListPool<FileInfo>.Get ())
			using (var invalidPrefabs = ListPool<FileInfo>.Get ()) {
				ResourcePathsByPathPrefix.FindAllPrefabs (resourcePath, prefabs);

				var oldIx = -1;

				for (var i = 0; i < prefabs.Count; i++) {
					var pName = Path.GetFileNameWithoutExtension (prefabs [i].Name);
					var pAsset = Resources.Load<GameObject> (string.Format ("{0}/{1}", resourcePath, pName));
					if (pAsset == null) {
						invalidPrefabs.Add (prefabs [i]);
						continue;
					}


				
					if (FindComponentWithTypeName(pAsset, pName) != null) {
						if (pName == selectedComponentTypeName) {
							oldIx = validPrefabs.Count;
						}
						validPrefabs.Add (prefabs [i]);
					} else {
						invalidPrefabs.Add (prefabs [i]);
					}
				
				}

				this.lastValidCount = validPrefabs.Count;

				using(var prefabNames = ArrayPool<string>.Get(validPrefabs.Count + 1)) {
					prefabNames.array [0] = "[none]";
					for (var nameIx = 0; nameIx < validPrefabs.Count; nameIx++) {
						prefabNames.array [nameIx + 1] = Path.GetFileNameWithoutExtension (validPrefabs [nameIx].Name);
					}

					curRect.y += baseHeight;

					oldIx += 1; // account for empty first entry

					GUI.color = validPrefabs.Count > 0 ? VALID : PENDING;

					var newIx = validPrefabs.Count > 0 ?
						EditorGUI.Popup (curRect, "Selected Resource Prefab (" + validPrefabs.Count + ")", oldIx, prefabNames.array) :
						EditorGUI.Popup (curRect, "No Valid Prefabs at Resource Path", oldIx, prefabNames.array);

					GUI.color = saveColor;

					if (newIx != oldIx) {
						if (newIx <= 0) {
							selectedComponentTypeProp.stringValue = "";
						} else {
							var selectedAssetName = Path.GetFileNameWithoutExtension (validPrefabs [newIx - 1].Name);
							var selectedAsset = Resources.Load<GameObject> (string.Format ("{0}/{1}", resourcePath, selectedAssetName));
							var c = FindComponentWithTypeName (selectedAsset, selectedAssetName);
							selectedComponentTypeProp.stringValue = c.GetType ().FullName;
						}
					}
				}

				if (invalidPrefabs.Count > 0) {
					GUI.color = PENDING;
					using (var invalidNames = ArrayPool<GUIContent>.Get (invalidPrefabs.Count)) {
						for (var nameIx = 0; nameIx < invalidPrefabs.Count; nameIx++) {
							invalidNames.array [nameIx] = new GUIContent(Path.GetFileNameWithoutExtension (invalidPrefabs [nameIx].Name));
						}

						curRect.y += baseHeight;

						EditorGUI.Popup (curRect,
							new GUIContent ("Unusable Prefabs", "These prefabs do not meet the requirement of having a Component whose type name matches the prefab name"), 
							0, invalidNames.array
						);
					}
					GUI.color = saveColor;
				}
			}

			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}

		private static Component FindComponentWithTypeName(GameObject go, string typeName)
		{
			using (var comps = ListPool<Component>.Get ()) {
				go.GetComponents<Component> (comps, true);
				return comps.Find (c => c.GetType ().Name == typeName);
			}
		}


	}
}



