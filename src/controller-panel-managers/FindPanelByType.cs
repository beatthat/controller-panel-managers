using UnityEngine;
using System.Collections.Generic;
using System;

namespace BeatThat
{
	public class FindPanelByType : MonoBehaviour
	{
		public Component FindPanel(Type pType)
		{
			Component p;
			GameObject pGO;
			if(m_managedPresentersByType.TryGetValue(pType, out pGO) && pGO != null && (p = pGO.GetComponent(pType)) != null) {
				return p;
			}
				
			foreach(Transform c in this.transform) {
				if((p = c.GetComponent(pType)) != null) {
					m_managedPresentersByType[pType] = p.gameObject;
					return p;
				}
			}

#if UNITY_EDITOR || APE_DEBUG_UNSTRIP
			Debug.LogWarning("Failed to find a managed panel of type '" + pType.Name + "'");
#endif
			return null;
		}

		private readonly Dictionary<Type, GameObject> m_managedPresentersByType = new Dictionary<Type, GameObject>(); 
	}
}
