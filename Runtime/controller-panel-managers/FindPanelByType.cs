using BeatThat.Placements;
using BeatThat.Properties;
using UnityEngine;
using System.Collections.Generic;
using System;
using BeatThat.FindResources;
using BeatThat.TransformPathExt;

namespace BeatThat.Controllers{
	public class FindPanelByType : MonoBehaviour
	{
		public bool m_ignoreUnmanagedPanels;
		public bool m_loadFromResources;
		[HideInInspector] [SerializeField] private bool m_forcePrefabsToDisabled = true;
		[HideInInspector] [SerializeField] private FindResourceByComponentType m_resourcePrefabs;


		[Tooltip("when a prefab is instantiated to edit, make sure the instance is active")]

#pragma warning disable 414
		[HideInInspector] [SerializeField] private bool m_onEditEnsureActive = true;
#pragma warning restore 414

		[Obsolete("use m_resourcePrefabs component property instead")] [HideInInspector] [SerializeField] private string m_loadFromResourcePath = "Panels";

		public string loadFromResourcesPath
		{
			get
			{
#pragma warning disable 618
				return !string.IsNullOrEmpty(m_resourcePrefabs.resourcePath) ? m_resourcePrefabs.resourcePath : m_loadFromResourcePath;
#pragma warning restore 618
			}
		}

		public FindResourceByComponentType resourcePrefabs { get { return m_resourcePrefabs; } }

		public bool supportsMultiplePrefabTypes { get { return true; } }

  
		public Component FindPanel(Type pType)
		{
			Component p;
			GameObject pGO;
			if (m_managedInstancesByType.TryGetValue(pType, out pGO) && pGO != null && (p = pGO.GetComponent(pType)) != null)
			{
				return p;
			}

			foreach (Transform c in this.transform)
			{
				if ((p = c.GetComponent(pType)) != null)
				{
					m_managedInstancesByType[pType] = p.gameObject;
					return p;
				}
			}

			if (m_loadFromResources)
			{
				string path = string.Format("{0}/{1}", this.loadFromResourcesPath ?? "", pType.Name);

				var asset = Resources.Load<GameObject>(path);

				if ((p = AddInstance(asset, pType, path)) != null)
                {
                    return p;
                }
			}

#if UNITY_EDITOR || DEBUG_UNSTRIP
			if (!m_ignoreUnmanagedPanels)
			{
				Debug.LogWarning("Failed to find a managed panel of type '" + pType.Name + "'");
			}
#endif
			return null;
		}

		private Component AddInstance(GameObject prefab, Type asType, string path = null)
		{
			if (prefab == null)
			{
				return null;
			}

            if (m_forcePrefabsToDisabled && prefab.activeSelf)
            {
#if UNITY_EDITOR
				if(path == null) {
					path = UnityEditor.AssetDatabase.GetAssetPath(prefab);
				}
#endif

#if UNITY_EDITOR || DEBUG_UNSTRIP
				Debug.LogWarning("[" + Time.frameCount + "] prefab for path  '" + path
                                 + "' should have its GameObject disabled on the asset. It will be set here which will modify the asset. If you don't want this behaviour, unset the " 
                                 + GetType() + ".forcePrefabsToDisabled property at " + this.Path());
#endif
                prefab.SetActive(false);
            }

            var pGO = Instantiate(prefab);
			Component inst;

			if ((inst = pGO.GetComponent(asType)) == null)
			{
				return null;
			}
            
			inst.transform.SetParent(this.transform, false);
            inst.name = asType.Name;
            PrefabPlacement.OrientToParent(inst.transform, prefab.transform);
            m_managedInstancesByType[asType] = inst.gameObject;
            return inst;
            
		}

#if UNITY_EDITOR
#pragma warning disable 618
        public void SyncForBackwardsCompatibility()
        {

            if (string.IsNullOrEmpty(m_resourcePrefabs.resourcePath) && !string.IsNullOrEmpty(m_loadFromResourcePath))
            {
                m_resourcePrefabs.resourcePath = m_loadFromResourcePath;
                m_loadFromResourcePath = "";
            }
        }
#pragma warning restore 618
#endif

		private readonly Dictionary<Type, GameObject> m_managedInstancesByType = new Dictionary<Type, GameObject>(); 
	}
}


