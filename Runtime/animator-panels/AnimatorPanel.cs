using BeatThat.GetComponentsExt;
using BeatThat.Panels;
using BeatThat.SafeRefs;
using BeatThat.StateControllers;
using BeatThat.TransformPathExt;
using BeatThat.Transitions;
using BeatThat.TypeUtil;
using UnityEngine;

namespace BeatThat.UI
{
    /// <summary>
    /// Panel that drives an Animator has these expectations for the Animator:
    /// 
    /// * has an <c>int</c> 'panelState' param 
    /// * has 'in' and 'out' target states on layer 0
    /// </summary>
    [RequireComponent(typeof(Animator))]
	public class AnimatorPanel : MonoBehaviour, Panel
	{
		[System.Serializable]
		public class TransitionTargetState
		{
			public TransitionTargetState(string t, int n) 
			{ 
				this.transition = t;
				this.stateId = n;
			}

			public TransitionTargetState() {}

			public string transition;
			public int stateId;
		}

		public int m_animatorTransitionLayer = 0;
		public string m_targetStateProperty = "panelState";
		public TransitionTargetState[] m_animatorTargetStates = {
			new TransitionTargetState(PanelTransition.OUT.name, 0),
			new TransitionTargetState(PanelTransition.IN.name, 1)
		};

		[System.Obsolete("if you really need this behaviour use StateControllerParams and configure to NOT reset on disable")]
		public bool m_preserveAnimatorStateAcrossDisable;

		public Transition PrepareTransition(PanelTransition t, OnTransitionFrameDelegate onFrameDel)
		{
			return PreparePanelTransition(t, onFrameDel);
		}

		public void StartTransition(PanelTransition t)
		{
//			Debug.Log ("[" + Time.time + "] StartTransition " + t);
			PreparePanelTransition(t, null).StartTransition();
		}

		public void BringInImmediate ()
		{
//			Debug.Log ("[" + Time.time + "] BringInImmediate ");
			var p = PrepareTransition(PanelTransition.IN, null);
			p.StartTransition();
			p.CompleteEarly();
		}

		public void DismissImmediate ()
		{
//			Debug.Log ("[" + Time.time + "] DismissImmediate ");
			var p = PrepareTransition(PanelTransition.OUT, null);
			p.StartTransition();
			p.CompleteEarly();
		}

		public Transition PreparePanelTransition(PanelTransition t, OnTransitionFrameDelegate onFrameDel)
		{
//			Debug.Log ("[" + Time.time + "] PreparePanelTransition " + t);
			var tr = new PanelAnimatorTransition(this, m_animatorTransitionLayer, t, onFrameDel);
			return tr;
		}

		private TransitionTargetState TargetForTransition(PanelTransition t)
		{
			foreach(var ts in m_animatorTargetStates) {
				if(ts.transition == t.name) {
					return ts;
				}
			}
			return null;
		}

		private void OnStart(PanelAnimatorTransition t)
		{
			if(this.activePanelTransition != null) {
				Debug.LogWarning("Starting panel transition " + t + " with transition " + this.activePanelTransition + " still running!");
				this.activePanelTransition.CompleteEarly();
				this.activePanelTransition = null;
			}

			var ts = TargetForTransition(t.panelTransition);
			if(ts == null) {
				Debug.LogWarning("No target for transition " + t.panelTransition + "!");
				t.Cancel();
				return;
			}

			this.activePanelTransition = t;

			if(this.stateParams.isReady) {
				this.stateParams.SetInt(m_targetStateProperty, ts.stateId);
			}
			else {
				m_pendingAnimatorStateUpdate = ts.stateId;
			}
		}

		private int m_pendingAnimatorStateUpdate = -1;

		void LateUpdate()
		{
			if(m_pendingAnimatorStateUpdate < 0) {
				return;
			}

			if(this.stateParams.isReady) {
				this.stateParams.SetInt(m_targetStateProperty, m_pendingAnimatorStateUpdate);
				m_pendingAnimatorStateUpdate = -1;
			}
		}


		private void OnComplete(PanelAnimatorTransition t)
		{
//			Debug.Log ("[" + Time.time + "] PanelAnimator::OnComplete " + t);
			if(this.activePanelTransition == t) {
				this.activePanelTransition = null;
			}
		}

		private void OnCancel(PanelAnimatorTransition t)
		{
//			Debug.Log ("[" + Time.time + "] PanelAnimator::OnCancel " + t);
			if(this.activePanelTransition == t) {
				this.activePanelTransition = null;
			}
		}

		void OnDestroy()
		{
			if(this.activePanelTransition != null && this.activePanelTransition.isTransitionRunning) {
				this.activePanelTransition.Cancel();
			}
		}

		private PanelAnimatorTransition activePanelTransition { get; set; }

		private Animator animator { get { return m_animator?? (m_animator = GetComponent<Animator>()); } }
		private Animator m_animator;

		private StateController stateParams 
		{
			get {
				if(m_stateParams == null) {
					if(m_preserveAnimatorStateAcrossDisable) {
						#if BT_DEBUG_UNSTRIP
						Debug.LogWarning("[" + Time.frameCount + "][" + this.Path() + "] using deprecated option preserveAnimatorStateAcrossDisable. If you really need this behaviour use StateControllerProperty components and configure to NOT reset on disable");
						#endif

						var scClass = TypeUtils.Find("BeatThat.AnimatorParamsSurviveDisable");
						if(scClass != null) {
							m_stateParams = this.gameObject.AddIfMissing(typeof(StateController), scClass) as StateController;

							if(m_stateParams != null) {
								return m_stateParams;
							}
						}
							
						#if BT_DEBUG_UNSTRIP
						Debug.LogWarning("[" + Time.frameCount + "][" + this.Path() + "] failed to find class BeatThat.AnimatorParamsSurviveDisable. Fallback to AnimatorController");
						#endif

					}
					m_stateParams = this.AddIfMissing<StateController, AnimatorController>();
				}
				return m_stateParams;
			}
		}
		private StateController m_stateParams;
		
		class PanelAnimatorTransition : TransitionBase
		{
			public PanelAnimatorTransition(AnimatorPanel owner, int transitionLayer, PanelTransition t, OnTransitionFrameDelegate onFrameDelegate)
			{
				m_owner = new SafeRef<AnimatorPanel>(owner);
				this.transitionLayer = transitionLayer;
				this.panelTransition = t;
				this.animatorTargetState = Animator.StringToHash(t.name);
				this.onFrameDelegate = onFrameDelegate;
				this.maxTransitionTime = 1f;
			}

			/// <summary>
			/// Safety measure: if the animator is for some reason NOT transitioning and it's not in the target state, 
			/// e.g. if the animator went transitioned to some state other than the target state, 
			/// we need to detect that and at least cancel.
			/// </summary>
			public float maxTransitionTime { get; set; }

			override public string ToString()
			{
				return "[PanelTransition owner=" + this.owner + ", t=" + this.panelTransition
					+ " running=" + this.isTransitionRunning + ", complete=" + this.isTransitionComplete + "]";
			}

			override protected void DoStartTransition(float time)
			{
//				Debug.Log ("[" + Time.time + "] PanelAnimator::DoStartTransition " + this);
				var o = this.owner;
				if(o == null) {
					Cancel();
					return;
				}

				o.OnStart(this);
			}

			override protected void DoUpdateTransition(float time, float deltaTime)
			{
				var a = this.animator;
				if(a == null || !a.isInitialized) {
					Cancel();
					return;
				}

				#if UNITY_EDITOR
				if(a.runtimeAnimatorController == null) {
					Debug.LogWarning("[" + Time.frameCount + "][" + this.owner.Path() + "] Animator has no controller!");
					Cancel();
					return;
				}
				#endif

				var stateInfo = a.GetCurrentAnimatorStateInfo(this.transitionLayer);

				if(stateInfo.shortNameHash == this.animatorTargetState) {
					Complete();
					return;
				}

				if(this.onFrameDelegate != null) {
					this.onFrameDelegate(Time.time - this.startTime, Time.time);
				}

				if(!a.IsInTransition(this.transitionLayer)) {
					if(time - this.startTime > this.maxTransitionTime) { // we're not transitioning and we're stopped, kill
						Cancel();
					}
				}

			}

			override protected void CompleteTransition()
			{
//				Debug.Log ("[" + Time.time + "] PanelAnimator::DoComplete " + this);
				if(this.owner == null) {
					return;
				}
				this.owner.OnComplete(this);
			}

			override protected void CompleteTransitionEarly()
			{
//				Debug.Log ("[" + Time.time + "] PanelAnimator::DoCompleteEarly " + this);
				var o = this.owner;
				if(o == null) {
					return;
				}
				o.animator.Play(this.animatorTargetState);
				o.OnComplete(this);
			}

			override protected void DoCancelTransition()
			{
//				Debug.Log ("[" + Time.time + "] PanelAnimator::DoCancel " + this);
				if(this.owner == null) {
					return;
				}
				this.owner.OnCancel(this);
			}
				
			public PanelTransition panelTransition { get; private set; }

			private AnimatorPanel owner 
			{
				get {
					return m_owner.value;
				}
			}
			
			private Animator animator 
			{ 
				get {
					return this.owner == null ? null : this.owner.animator;
				}
			}

			private OnTransitionFrameDelegate onFrameDelegate { get; set; }

			private int animatorTargetState { get; set; }
			private int transitionLayer { get; set; }


			private SafeRef<AnimatorPanel> m_owner;
		}



	}
}
