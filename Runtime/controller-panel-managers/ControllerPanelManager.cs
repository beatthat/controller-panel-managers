using System;
using System.Collections.Generic;
using BeatThat.CollectionsExt;
using BeatThat.GetComponentsExt;
using BeatThat.Panels;
using BeatThat.Pools;
using BeatThat.Requests;
using BeatThat.SafeRefs;
using BeatThat.Transitions;
using UnityEngine;

namespace BeatThat.Controllers
{
    /// <summary>
    /// A PanelManager that supports panels that are IController and/or HasPanelTransitions types.
    /// 
    /// For IController panels, makes sure (optional) model is bound and IController::Go is called when an IController panel opens,
    /// and similarly, makes sure Unbind is called when an IController panel closes.
    /// 
    /// For HasPanelTransitions panels, uses HasPanelTransitions::TransitionIn and HasPanelTransitions::TransitionOut
    /// methods to open and close the panel.
    /// </summary>
    public class ControllerPanelManager : MonoBehaviour, PanelManager 
	{
		public bool m_suspend;
		public bool m_allowChangeToSelf = false;

		public bool m_ignoreUnmanagedPanels;

		/// <summary>
		/// When true, signals transitions managed by this presenter to enable their debug logging
		/// </summary>
		public bool m_debugTransitions;

		public bool allowChangeToSelf 
		{
			get { return m_allowChangeToSelf; }
			set { m_allowChangeToSelf = value;	}
		}

		private IDisposable TransitionBlock()
		{
            return m_debugTransitions? RequestConfig.DebugStart(): RequestConfig.DebugDisabled();
		}

		public PanelDisplayController displayController
		{
			get { return m_displayController?? (m_displayController = GetComponent<PanelDisplayController>()); }
			set { m_displayController = value; }
		}

		public void ClosePanel(bool showPreviousPanel = true)
		{
			ChangePanel(new ChangePanel(null, null), false, showPreviousPanel);
		}

		public void ClosePanel(Type panelType, bool showPreviousPanel = true)
		{
			if (this.activePanel != null && this.activePanel.GetComponent (panelType) != null) {
				ClosePanel (this.activePanel, showPreviousPanel);
			}
		}

		public void ClosePanel(GameObject panelGO, bool showPreviousPanel = true)
		{
			if(this.activePanel == panelGO) {
				ClosePanel(showPreviousPanel);
				return;
			}

			if(panelGO == null) {
				return;
			}

			var pCtl = panelGO.GetComponent<IController>();
			if(pCtl == null) {
				panelGO.SetActive(false);
				RemoveClosedPanelFromStack(panelGO);
				return;
			}

			// TODO: need to think of all the scenarios and test
			var hasT = pCtl as HasPanelTransitions;
			if(hasT != null) {
				hasT.TransitionOut(true);
			}
			else {
				pCtl.Unbind();
				RemoveClosedPanelFromStack(panelGO);
			}
		}

		public void GetPanelStack(List<GameObject> panelStack)
		{
			for(int i = m_panelStack.Count - 1; i >= 0; i--) {
				var p = m_panelStack[i];
				if(p == null) { // panel that destroyed itself?
					m_panelStack.RemoveAt(i);
					continue;
				}

				panelStack.Insert(0, p);
			}
		}
		
		public GameObject activePanel
		{
			get {
				for(int i = m_panelStack.Count - 1; i >= 0; i--) {
					var p = m_panelStack[i];
					if(p != null) { // null panel that destroyed itself?
						return p;
					}
				}
				return null;
			}
		}
		
		public bool hasActivePanel 
		{
			get {
				return this.activePanel != null;
			}
		}
		
		public bool isActivePanelInOrTransitioningIn
		{
			get {
				var p = this.activePanel;
				if(p == null) {
					return false;
				}
				
				HasPanelTransitions hasTrans = p.GetComponent<HasPanelTransitions>();
				
				if(hasTrans == null) {
					return p.activeSelf;
				}
				
				return(hasTrans.panelState == PanelTransitionState.IN 
					|| hasTrans.panelState == PanelTransitionState.TRANSITIONING_IN);
			}
		}

		/// caller should set p.model before calling
		public void ChangePanel(ChangePanel req, bool skipTransitions = false, bool showPreviousPanel = false)
		{
			// need to extract anything from the options now in case they will get disposed (are poolable)
			// because below we will do async stuff (transitions)
			var model = ControllerPanelOptions.OPT_MODEL.Get (req.options);
				
			using(var debug = TransitionBlock()) {
				if(!ResolvePanel(ref req)) {
					// this request specified a panel by presenter type but that panel is not managed here...
					#if UNITY_EDITOR || DEBUG_UNSTRIP
					if(!m_ignoreUnmanagedPanels) {
						Debug.LogWarning("[" + Time.frameCount + "] " + GetType() + "::ChangePanel called for an unmanaged panel type: " + req.panelType);
					}
					#endif
					return;
				}

				var newPanel = req.panelGO;
				var oldPanel = this.activePanel;

				var newPanelSameAsActive = (oldPanel == newPanel);

				if(newPanelSameAsActive && !this.allowChangeToSelf) {
					return;
				}

				if(newPanel != null) {
					RemovePanelFromStack(newPanel);// in case it's already there in the background
				}

				showPreviousPanel |= req.push;
				
				if(m_activeTransition != null) {
					m_activeTransition.CompleteEarly();
					m_activeTransition = null;
				}
				
				var t = new ChainTransition(); // TODO: reuse the transition
				
				if(newPanel != null) {

					var newPanelHasTrans = newPanel.GetComponent<HasPanelTransitions>();

					// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
					// THIS UNBIND BLOCK MAY BE A SOURCE OF PROBLEMS
					// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
					//
					// Was commented out (I think to fix an issue with suspend in mobile app)
					// ...then noticed it breaks browse moves feature in legacy/home...
					//
					// Leaving uncommented for now. Long term, this whole impl should die.
					if(!(newPanelSameAsActive && newPanelHasTrans != null)) {
						// LINE COMMENTED BELOW MAY BREAK SOME LEGACY POLARIS SCREENS (Move window that transitions to itself)
//						newPanel.Unbind(); // if p is already the active panel, allow transitions to handle the unbind
					}

					if(oldPanel != null) {
						if(!showPreviousPanel) {
							t = TransitionOutOrClose(oldPanel, t);
						}
						else if(m_suspend) {
							OnSuspend (oldPanel, true);
//							oldPanel.SetBool<Suspend>(true, MissingComponentOptions.Cancel);
						}
					}

					// push popup and make it the 'activePanel' *before* activating, 
					// so that in the case the presenter calls close popup on itself 
					// during its activation, there's a popup here to unbind/deactivate
					m_panelStack.Add(newPanel);

					// Adding a new panel to the stack
					if(newPanelHasTrans != null) {
						var newPanelGO = newPanel.gameObject;
						t.AddAction(() => { // TODO: store and resuse action
							
							// make sure the new panel is top of the stack. 
							// it could have gotten removed if we transitioned the same panel out and then back in
							if(this.activePanel != newPanel) {
								RemovePanelFromStack(newPanel);
								m_panelStack.Add(newPanel);
							}

							ActivatePanel(req.panelGO, req.panel, model); //req.Go();
							UpdateDisplay();
						});

						t.Add(newPanelHasTrans.EnsureTransitionIn()); // TODO: store and resuse action
							
						if(m_suspend) {
							t.AddAction(() => { if(newPanelGO != null) { 
									OnSuspend (newPanelGO, false);
//									newPanelGO.SetBool<Suspend> (false); 
								}
							});
						}
					}
					else {
						ActivatePanel(req.panelGO, req.panel, model);//req.Go();
					}
				}
				else {
					// Hide the active panel
					var nextPanel = (showPreviousPanel)? PanelBehind(this.activePanel): null;
					t = TransitionOutOrClose(oldPanel, t);
					
					if(showPreviousPanel) {	
						if(nextPanel != null) {
							nextPanel.gameObject.SetActive(true);

							if(m_suspend) {
								OnSuspend (nextPanel, false);
//								nextPanel.SetBool<Suspend>(false, MissingComponentOptions.Cancel);
							}
						}
						else {
							m_panelStack.Clear();
						}
					}
					else {
						m_panelStack.Clear();
					}

					UpdateDisplay();
				}
				
				if(t.subtransitionCount > 0) {
					t.AddAction(() => {
						if(m_activeTransition != null) {
							if(m_activeTransition.isTransitionRunning) {
								m_activeTransition.CompleteEarly();
							}
							
							m_activeTransition = null;
						}

						UpdateDisplay();
					});
					
					m_activeTransition = t;
					t.StartTransition();
					
					if(skipTransitions) {
						t.CompleteEarly();
					}
				}
			}
		}

		private GameObject PanelBehind(GameObject p)
		{
			if(p == null) {
				return null;
			}

			var i = m_panelStack.Count - 1;
			for(; i >= 0; i--) {
				if(m_panelStack[i] == p) {
					i -= 1; // next panel
					break;
				}
			}

			for(; i >= 0; i--) {
				if(m_panelStack[i] != null) {
					return m_panelStack[i];
				}
			}

			return null;

		}
		
		// Puts the active popup in front of the scrim, and the other popups behind the scrim in order
		private void UpdateDisplay()
		{
			m_updateDisplay = true;
		}

		public void ActivatePanel(GameObject panelGO, object panel, object model)
		{
			if(panelGO == null) {
				return;
			}

			var ctl = panel as IController;
			if(ctl != null) {
				ActivateController(ctl, model);
				return;
			}

			panelGO.SetActive(true);

			if((ctl = panelGO.GetComponent<IController>()) != null) {
				ActivateController(ctl, model);
				return;
			}
		}

		private static void ActivateController(IController c, object model)
		{
			var hasModel = c as HasModel;
			if(hasModel != null) {
				hasModel.GoWithModel(model);
			}
			else {
				c.ResetBindGo();
			}
		}
		
		private ChainTransition TransitionOutOrClose(GameObject p, ChainTransition t)
		{
			if(p == null) {	
				return t;
			}

			var hasPanelTransitions = p.GetComponent<HasPanelTransitions>();
			if(hasPanelTransitions != null) {
				t.Add(hasPanelTransitions.PrepareTransitionOut(true));
				t.AddAction(() => {
					RemoveClosedPanelFromStack(p.gameObject);
					UpdateDisplay();
				});
				return t;
			}

			var ctl = p.GetComponent<IController>();
			if(ctl != null) {
				ctl.Unbind();
				RemoveClosedPanelFromStack(p.gameObject);
				return t;
			}

			p.SetActive(false);
			return t;
		}

		private void RemovePanelFromStack(GameObject p)
		{
			for(int i = m_panelStack.Count - 1; i >= 0; i--) {
				if(object.ReferenceEquals(m_panelStack[i], p)) {
					m_panelStack.RemoveAt(i);
				}
			}
		}

		private void RemoveClosedPanelFromStack(GameObject p)
		{
			RemovePanelFromStack(p);
			UpdateDisplay();
		}

		private void DoUpdateDisplay()
		{
			using(var displayList = ListPool<Transform>.Get()) {	
			
				if(m_panelStack != null) {
					foreach(var p in m_panelStack) {
						if(p == null) {
							continue;
						}
						displayList.Add(p.transform);
					}
				}
				
				if(this.displayController != null) {
					this.displayController.UpdateDisplayList(displayList);
				}
			}
		}
		
		private bool ResolvePanel(ref ChangePanel openReq)
		{
			if(openReq.panel != null) {
				return true;
			}

			if(openReq.panelType == null) {
				// this is a close panel request
				return true;
			}

			var p = this.findPanelByType.FindPanel(openReq.panelType); //ResolvePanel(openReq.presenterType);
			if(p == null) {
				return false;
			}

			openReq = openReq.ResolvedPanel(p);
			return true;
		}

		void LateUpdate()
		{
			if(m_updateDisplay) {
				DoUpdateDisplay();
				m_updateDisplay = false;
			}
		}

		private void OnSuspend(GameObject go, bool s)
		{
			var h = m_handlesPanelSuspend.value;

			if (h == null && this.hasTriedFindHandlePanelSuspend) {
				return;
			}

			h = (m_suspend) ? this.AddIfMissing<IHandlePanelSuspend, ManagePanelSuspendParam> () : GetComponent<IHandlePanelSuspend> ();
			m_handlesPanelSuspend = new SafeRef<IHandlePanelSuspend>(h);

			this.hasTriedFindHandlePanelSuspend = true;

			if (h == null) {
				return;
			}

			if (s) {
				h.OnSuspend (go);
			}
			else {
				h.OnUnsuspend(go);
			}

		}

		private bool hasTriedFindHandlePanelSuspend { get; set; }
		private SafeRef<IHandlePanelSuspend> m_handlesPanelSuspend;

		private FindPanelByType findPanelByType { get { return m_findPanelByType?? (m_findPanelByType = this.AddIfMissing<FindPanelByType>()); } }
		private FindPanelByType m_findPanelByType;
		private bool m_updateDisplay;
		private Transition m_activeTransition;
		private PanelDisplayController m_displayController;

//		private Dictionary<Type, SafeRef<IController>> m_managedPresentersByType = new Dictionary<Type, SafeRef<IController>>(); 
		
		public List<GameObject> m_panelStack = new List<GameObject>();
	}
}





