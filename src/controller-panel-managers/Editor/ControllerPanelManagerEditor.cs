using UnityEngine;
using UnityEditor;
using BeatThat;

namespace BeatThat.UI
{
	[CustomEditor(typeof(ControllerPanelManager), true)]
	public class ControllerPanelManagerEditor : UnityEditor.Editor 
	{
//		private bool showProperties { get; set; }

		override public void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			PresentPanelStack();

			this.serializedObject.ApplyModifiedProperties();
		}


		private void PresentPanelStack()
		{
			if(!Application.isPlaying) {
				return;
			}

			var pm = this.target as ControllerPanelManager;

			GUI.enabled = false;
			if(pm.activePanel != null) {
				EditorGUILayout.ObjectField("Active Panel", pm.activePanel as Object, typeof(Component), true);
			}
			else {
				EditorGUILayout.LabelField("Active Panel: NONE");
			}

			using(var panelStack = ListPool<GameObject>.Get()) {
				pm.GetPanelStack(panelStack);

				if(panelStack.Count == 0) {
					EditorGUILayout.LabelField("Panel Stack: EMPTY");
					return;
				}

				EditorGUILayout.LabelField("Panel Stack");

				EditorGUI.indentLevel++;
				for(int i = panelStack.Count - 1; i >= 0; i--) {
					EditorGUILayout.ObjectField(panelStack[i] as Object, typeof(Component), true);
				}
				EditorGUI.indentLevel--;
			}

			GUI.enabled = true;

		}


	}
}
