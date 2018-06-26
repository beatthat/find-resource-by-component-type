using System;
using System.Collections.Generic;
using System.IO;
using BeatThat.GetComponentsExt;
using BeatThat.ManagePrefabInstances;
using BeatThat.Pools;
using BeatThat.TypeUtil;
using UnityEngine;

namespace BeatThat.FindResources
{
    [Serializable]
	public class FindResourceByComponentType
	{
		[HideInInspector] [SerializeField] private string m_resourcePath = "";
		[HideInInspector] [SerializeField] private string m_selectedComponentType = ""; // Full type name of the selected asset

		public string resourcePath { get { return m_resourcePath; } set { m_resourcePath = value; } }

		public Component GetSelectedAsset()
		{
			var type = GetSelectedType();
			return type != null ? FindAssetByType(type) : null;
		}

		public Type GetSelectedType()
		{
			return string.IsNullOrEmpty(m_selectedComponentType) ? null : TypeUtils.Find(m_selectedComponentType);
		}

		public Component FindAssetByType(Type pType)
		{
			string path = string.Format("{0}/{1}", m_resourcePath ?? "", pType.Name);
			var asset = Resources.Load<GameObject>(path);
			return asset != null ? asset.GetComponent(pType) : null;
		}

#if UNITY_EDITOR
		public void FindAllPrefabs(ICollection<PrefabType> prefabs, PrefabInstancePolicy defaultInstancePolicy)
		{
			using(var prefabsAssets = ListPool<GameObject>.Get()) {
				FindAllPrefabs(prefabsAssets);

				foreach(var p in prefabsAssets) {

                    // the convention/rule of FindResourcesByComponentType is that the prefab must have a component whose type matches its file name

					var type = TypeUtils.Find(p.name);
					if(type == null) {
						continue;
					}
					var c = p.GetComponent(type);
					if(c == null) {
						continue;
					}

					prefabs.Add(new PrefabType
					{
						prefab = c,
						prefabType = type,
						instancePolicy = defaultInstancePolicy
					});
				}
			}
		}

		public void FindAllPrefabs(ICollection<GameObject> prefabs) 
		{
			using (var prefabFiles = ListPool<FileInfo>.Get())
			{
				ResourcePathsByPathPrefix.FindAllPrefabs(resourcePath, prefabFiles);
    
				for (var i = 0; i < prefabFiles.Count; i++)
				{
					var pName = Path.GetFileNameWithoutExtension(prefabFiles[i].Name);
					var pAsset = Resources.Load<GameObject>(string.Format("{0}/{1}", resourcePath, pName));
					if (pAsset == null)
					{
						continue;
					}

					if (FindComponentWithTypeName(pAsset, pName) == null)
					{
						continue; // this prefab doesn't have a component whose type matches its name, so ignore it
					}

					prefabs.Add(pAsset);

				}
			}
        }

        private static Component FindComponentWithTypeName(GameObject go, string typeName)
        {
            using (var comps = ListPool<Component>.Get())
            {
                go.GetComponents<Component>(comps, true);
                return comps.Find(c => c.GetType().Name == typeName);
            }
        }

#endif
	}
}


