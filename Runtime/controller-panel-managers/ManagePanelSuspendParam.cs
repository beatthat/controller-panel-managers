using BeatThat.Properties;
using UnityEngine;

namespace BeatThat.Controllers{
    public class ManagePanelSuspendParam : MonoBehaviour, IHandlePanelSuspend
	{
		#region IHandlePanelSuspend implementation

		public void OnUnsuspend (GameObject go)
		{
			go.SetBool<Suspend>(false, MissingComponentOptions.Cancel);
		}

		public void OnSuspend (GameObject go)
		{
			go.SetBool<Suspend>(true, MissingComponentOptions.Cancel);
		}

		#endregion



	}
}
