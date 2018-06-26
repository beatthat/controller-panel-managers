using BeatThat.Placements;
using BeatThat.Pools;
using System.IO;
using BeatThat.ManagePrefabInstances;
using UnityEditor;
using UnityEngine;

namespace BeatThat.Controllers
{
    [CustomEditor(typeof(FindPanelByType))]
	public class FindPanelByTypeEditor : UnityEditor.Editor 
	{

		void OnEnable()
		{
			var resourcePaths = Directory.GetDirectories (Application.dataPath, "Resources", SearchOption.AllDirectories);
				
			var panelPathFromResource = (this.target as FindPanelByType).loadFromResourcesPath;

			using (var panelResourcePathList = ListPool<string>.Get ()) {
				foreach (var rp in resourcePaths) {
					var panelPath = Path.Combine (rp, panelPathFromResource);
					if (!Directory.Exists (panelPath)) {
						continue;
					}
					panelResourcePathList.Add (panelPath);
				}
				this.panelResourcePaths = panelResourcePathList.ToArray ();
			}
		}

		override public void OnInspectorGUI()
		{
			(this.target as FindPanelByType).SyncForBackwardsCompatibility ();

			base.OnInspectorGUI();
			var loadFromResourcesProp = this.serializedObject.FindProperty ("m_loadFromResources");
			if (loadFromResourcesProp.boolValue) {

				var resourcePrefabsProp = this.serializedObject.FindProperty ("m_resourcePrefabs");

				EditorGUILayout.PropertyField (resourcePrefabsProp);

				var forcePrefabsToDisabledProp = this.serializedObject.FindProperty ("m_forcePrefabsToDisabled");
				EditorGUILayout.PropertyField (forcePrefabsToDisabledProp, new GUIContent (
				"Force Prefabs Disabled", "If TRUE then at runtime a prefab's GameObject will be set enable=false before instantiate. This is to prevent OnEnable, Awake, etc from executing."));
			}

            this.serializedObject.ApplyModifiedProperties();

			if (loadFromResourcesProp.boolValue && !Application.isPlaying) {
				ShowEditPrefabsOption ();
			}
		}

		private int selectedPrefabIndex { get; set; }

        
		private Component CreateInstance(Component prefab, bool ensureActive = false)
		{
			var inst = (UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as Component).transform;
            inst.name = prefab.name;
            inst.transform.SetParent((this.target as Component).transform, false);

            PrefabPlacement.OrientToParent(inst.transform, prefab.transform);

			if(ensureActive && !inst.gameObject.activeSelf) {
				inst.gameObject.SetActive(true);
			}


			return inst;
		}

		private void ShowEditPrefabsOption()
		{
			var bkgColorSave = GUI.backgroundColor;

			var prefab = (this.target as FindPanelByType).resourcePrefabs.GetSelectedAsset ();
			if (prefab == null) {
				return;
			}

			var forceActiveProp = this.serializedObject.FindProperty("m_onEditEnsureActive");
			EditorGUILayout.PropertyField(forceActiveProp);
            this.serializedObject.ApplyModifiedProperties();

			GUI.backgroundColor = Color.green;
			if(GUILayout.Button("Edit Prefab")) {
				CreateInstance(prefab, forceActiveProp.boolValue);
			}

			if (prefab.GetComponent<ManagesPrefabInstances>() != null && GUILayout.Button("Edit Prefab and Nested Prefabs"))
            {
				var inst = CreateInstance(prefab, forceActiveProp.boolValue);
				var mpi = inst.GetComponent<ManagesPrefabInstances>();
				mpi.EditPrefabRecursive();
            }

			GUI.backgroundColor = bkgColorSave;
		}


		private string[] panelResourcePaths { get; set; }

	}
}



