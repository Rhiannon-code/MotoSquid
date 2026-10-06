using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberRevolution.BreakableObjectsSystem.Examples {

	public class ExamplesSwitcher : MonoBehaviour {

		public void SwitchToExample(int index) {
#if UNITY_EDITOR
			UnityEditor.SceneManagement.EditorSceneManager
				.LoadSceneInPlayMode($"Assets/CyberRevolution/BreakableObjectsSystem/Examples/Example{index}/Example{index}.unity",
					new LoadSceneParameters(LoadSceneMode.Single));
#else
			SceneManager.LoadScene($"Example{index}");
#endif
		}

	}

}