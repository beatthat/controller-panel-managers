using BeatThat.Pools;
using BeatThat.Properties;
using BeatThat;
using System.Collections.Generic;
using BeatThat.Panels;

namespace BeatThat.Controllers{
	public static class ControllerPanelNotifications  
	{
		public static void Open(object panel, object model, IDictionary<string, object> options = null)
		{
			if(options != null) {
				PanelNotifications.Open(NewChangePanel(panel, model, options));
				return;
			}

			using(var opts = DictionaryPool<string, object>.Get()) {
				PanelNotifications.Open(NewChangePanel(panel, model, opts));
			}
		}

		public static void Open<T>(object model, IDictionary<string, object> options = null) where T : IController
		{
			if(options != null) {
				PanelNotifications.Open(NewChangePanel<T>(model, options));
				return;
			}

			using(var opts = DictionaryPool<string, object>.Get()) {
				PanelNotifications.Open(NewChangePanel<T>(model, opts));
			}
		}

		public static ChangePanel NewChangePanel(object panel, object model, IDictionary<string, object> options = null)
		{
			if(model == null) {
				return new ChangePanel(panel, options);
			}

			options = options?? DictionaryPool<string, object>.Get(); // functions like a simple new even if no return
			ControllerPanelOptions.OPT_MODEL.Set(model, options);
			return new ChangePanel(panel, options);
		}

		public static ChangePanel NewChangePanel<T>(object model, IDictionary<string, object> options = null)
		{
			if(model == null) {
				return ChangePanel.OfType<T>(options);
			}

			options = options?? DictionaryPool<string, object>.Get(); // functions like a simple new even if no return
			ControllerPanelOptions.OPT_MODEL.Set(model, options);
			return ChangePanel.OfType<T>(options);
		}
	}
}


