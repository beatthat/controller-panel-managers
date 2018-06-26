using BeatThat.Properties;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BeatThat.Controllers{
	/// <summary>
	/// ControllerPanelManager will use a (found) sibling instance of IHandleActivePanelStatus
	/// to delegate any behaviour that happens when a panel becomes the active panel or gets pushed back in the stack.
	/// 
	/// A common implementation is to notify that panel that it is either suspended (or unsuspended)
	/// </summary>
	public interface IHandlePanelSuspend 
	{
		void OnUnsuspend (GameObject go);

		void OnSuspend(GameObject go);

	}
}
