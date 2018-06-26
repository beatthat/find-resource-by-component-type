#if UNITY_EDITOR
using BeatThat.Pools;
using BeatThat.CollectionsExt;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BeatThat.FindResources
{
    public static class ResourcePathsByPathPrefix
	{
		public static void InvalidatePaths(string[] paths)
		{
			if (m_resourcePathsByPathPrefix == null)
            {
                return;
            }

            using (var invalidatePathPrefixes = ListPool<string>.Get())
            {
                foreach (var pathPrefix in m_resourcePathsByPathPrefix.Keys)
                {
                    foreach (var pSaved in paths)
                    {
                        if (!pSaved.Contains("Resources"))
                        {
                            continue;
                        }
                        if (pSaved.Contains(Path.Combine("Resources", pathPrefix)))
                        {
                            invalidatePathPrefixes.Add(pathPrefix);
                        }
                    }
                }

                foreach (var invalidatedPath in invalidatePathPrefixes)
                {
                    m_resourcePathsByPathPrefix.Remove(invalidatedPath);
                }
            }
		}

		public static void FindResourcePaths(string pathPrefix, ICollection<string> paths)
		{
			string[] pathsInternal;
			m_resourcePathsByPathPrefix = m_resourcePathsByPathPrefix ?? new Dictionary<string, string[]>();

			if (m_resourcePathsByPathPrefix.TryGetValue(pathPrefix, out pathsInternal))
			{
				paths.AddRange(pathsInternal);
				return;
			}

			var resourcePaths = Directory.GetDirectories(Application.dataPath, "Resources", SearchOption.AllDirectories);

			using (var pathList = ListPool<string>.Get())
			{
				foreach (var rp in resourcePaths)
				{
					var panelPath = Path.Combine(rp, pathPrefix);
					if (!Directory.Exists(panelPath))
					{
						continue;
					}
					pathList.Add(panelPath);
				}

				m_resourcePathsByPathPrefix[pathPrefix] = pathList.ToArray();

				paths.AddRange(pathList);
			}
		}

		public static void FindAllPrefabs(string pathPrefix, List<FileInfo> prefabs)
		{
			using (var pathList = ListPool<string>.Get())
			{
				FindResourcePaths(pathPrefix, pathList);

				foreach (var rp in pathList)
				{
					foreach (var p in Directory.GetFiles(rp, "*.prefab"))
					{
						prefabs.Add(new FileInfo(p));
					}
				}

				prefabs.Sort((x, y) => string.Compare(x.Name, y.Name, System.StringComparison.CurrentCulture));
			}
		}


		private static Dictionary<string, string[]> m_resourcePathsByPathPrefix;
	}
}

#endif


