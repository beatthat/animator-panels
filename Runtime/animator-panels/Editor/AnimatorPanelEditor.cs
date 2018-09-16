using UnityEditor;
using UnityEngine;

namespace BeatThat.UI
{

	[CustomEditor(typeof(AnimatorPanel))]
	public class AnimatorPanelEditor : UnityEditor.Editor
	{
		
		private AnimatorPanel panelAnimator { get; set; }
		private SerializedProperty preserveProp { get; set; }

		void OnEnable()
		{
			this.panelAnimator = this.target as AnimatorPanel;
			this.preserveProp = this.serializedObject.FindProperty("m_preserveAnimatorStateAcrossDisable");
		}
			
		public override void OnInspectorGUI()
		{	
			// TODO: convert all prop handling to serializedProperty
			this.serializedObject.Update();

			var animator = this.panelAnimator.GetComponent<Animator>();

			if(animator == null) {
				Error("Missing required Animator component!");
				return;
			}

			if(!(animator.gameObject.activeSelf && animator.enabled)) {
				Warn("Enable gameobject and animator to view/edit properties");
				return;
			}

			EditorGUILayout.PropertyField(preserveProp, new GUIContent("Preserve Animator State Across Disable", "[obsolete]user StateControllerParams instead"));


			var layerProp = this.serializedObject.FindProperty("m_animatorTransitionLayer");
			int tLayer = EditorGUILayout.IntField("Transition Layer", layerProp.intValue);
			if(tLayer != layerProp.intValue) {
				layerProp.intValue = tLayer;
			}

			if(animator.layerCount <= tLayer) {
				Error("Transition layer " + tLayer  + " missing from animator");
			}

			var statePropName = EditorGUILayout.TextField("Transition State Property", this.panelAnimator.m_targetStateProperty).Trim();
			this.panelAnimator.m_targetStateProperty = statePropName;


			if(string.IsNullOrEmpty(this.panelAnimator.m_targetStateProperty)) {
				Error("Animator needs an (int) state property");
				return;
			}

			var stateProp = GetProperty(statePropName, animator);
			if(stateProp == null) {
				Error("Configured state property '" + statePropName + "' not found");
				return;
			}

			if(stateProp.type != AnimatorControllerParameterType.Int) {
				Error("Configured state property '" + statePropName + "' must be of type int");
				return;
			}

			
//			var ttsList = pa.m_animatorTargetStates;
			
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Transition Target States");
			var ttsListProp = this.serializedObject.FindProperty("m_animatorTargetStates");
			var size = EditorGUILayout.IntField(ttsListProp.arraySize);
			if(size != ttsListProp.arraySize) {
				ttsListProp.arraySize = size;
			}
			EditorGUILayout.EndHorizontal();

			var bkgColor = GUI.backgroundColor;
			for(int i = 0; i < size; i++) {
				GUI.backgroundColor = bkgColor;

				var ttsProp = ttsListProp.GetArrayElementAtIndex(i);
				var transition = ttsProp.FindPropertyRelative("transition").stringValue;
		
				bool stateMissing = !animator.HasState(this.panelAnimator.m_animatorTransitionLayer, Animator.StringToHash(transition));
				if(stateMissing) {
					GUI.backgroundColor = Color.red;
				}
				EditorGUILayout.PropertyField(ttsProp, true);

				if(stateMissing) {
					Error("No target state '" + transition + "' in animator layer " + this.panelAnimator.m_animatorTransitionLayer);
				}

			}

			if(GUI.changed)	{
				this.serializedObject.ApplyModifiedProperties();
				EditorUtility.SetDirty(this.target);
			}
		}

		private static AnimatorControllerParameter GetProperty(string pName, Animator a)
		{
			foreach(var p in a.parameters) {
				if(p.name == pName) {
					return p;
				}
			}
			return null;
		}

		private void Error(string error)
		{
			var c = GUI.color;
			GUI.color = Color.red;
			EditorGUILayout.LabelField(error);
			GUI.color = c;
		}

		private void Warn(string error)
		{
			var c = GUI.color;
			GUI.color = Color.yellow;
			EditorGUILayout.LabelField(error);
			GUI.color = c;
		}

	}
}

