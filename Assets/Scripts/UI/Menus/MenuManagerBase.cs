using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.UI
{
    public abstract class MenuManagerBase : MonoBehaviour
    {
        protected void ShowPanel(UIPopup popup)
        {
            if (popup == null) return;
            popup.gameObject.SetActive(true);
            popup.PlayIn();
        }
        protected void HidePanel(UIPopup popup) { if (popup != null) popup.PlayOut(); }
        protected void LoadScene(string sceneName) { SceneManager.LoadScene(sceneName); }
    }
}
