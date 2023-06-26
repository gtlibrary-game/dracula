// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

public partial class UILogin : MonoBehaviour
{
    public UIPopup uiPopup;
    public NetworkManagerMMO manager; // singleton=null in Start/Awake
    public NetworkAuthenticatorMMO auth;
    public GameObject panel;
    public Text statusText;
    public InputField accountInput;
    public InputField passwordInput;
    public Dropdown serverDropdown;
    public Button loginButton;
    public Button registerButton;
    [TextArea(1, 30)] public string registerMessage = "First time? Just log in and we will\ncreate an account automatically.";
    public Button hostButton;
    public Button dedicatedButton;
    public Button cancelButton;
    public Button quitButton;
    public Button resetPasswordButton;
    public Button pasteButton;
    public NFTManager nftManager;

    public TextMeshProUGUI userNameInput;
    public TextMeshProUGUI userPassInput;
    void Start()
    {
        // load last server by name in case order changes some day.
        if (PlayerPrefs.HasKey("LastServer"))
        {
            string last = PlayerPrefs.GetString("LastServer", "");
            serverDropdown.value = manager.serverList.FindIndex(s => s.name == last);
        }
        //Invoke("ConnectServer", 2f);
    }
    /* Dont start server until login
    void ConnectServer()
    {
        if (manager.state == NetworkState.Offline || manager.state == NetworkState.Handshake)
        {
            manager.StartClient();
        }
    }*/
    void OnDestroy()
    {
        // save last server by name in case order changes some day
        PlayerPrefs.SetString("LastServer", serverDropdown.captionText.text);
    }

    void Update()
    {
        // only show while offline
        // AND while in handshake since we don't want to show nothing while
        // trying to login and waiting for the server's response
        if (manager.state == NetworkState.Offline || manager.state == NetworkState.Handshake)
        {
            
            panel.SetActive(true);

            // status
            if (NetworkClient.isConnecting)
                statusText.text = "Connecting...";
            else if (manager.state == NetworkState.Handshake)
                statusText.text = "Handshake...";
            else
                statusText.text = "";

            // buttons. interactable while network is not active
            // (using IsConnecting is slightly delayed and would allow multiple clicks)
            registerButton.interactable = !manager.isNetworkActive;
            registerButton.onClick.SetListener(() => { 
                auth.RegisterEmail();
             });

            resetPasswordButton.interactable = manager.isNetworkActive;
            resetPasswordButton.onClick.SetListener(() => { auth.ResetPassword(); });
            loginButton.interactable = !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);
            // loginButton.interactable = !manager.isNetworkActive && auth.IsAllowedAccountName(userNameInput.text);
            loginButton.interactable = nftManager.walletFlg;
            loginButton.onClick.SetListener(() => {
                // auth.LoginFlg = true;
                
                auth.LoginUser();
            });

            pasteButton.interactable = !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);
            pasteButton.onClick.SetListener(() => {
                string javascript = @"
                    navigator.clipboard.readText().then(function(text) {
                        unityInstance.SendMessage('ClipboardButton', 'OnClipboardRead', text);
                    }).catch(function(error) {
                        unityInstance.SendMessage('ClipboardButton', 'OnClipboardError', error);
                    });
                ";
                Application.ExternalEval(javascript);
            });

            

            hostButton.interactable = Application.platform != RuntimePlatform.WebGLPlayer && !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);
            hostButton.interactable = Application.platform != RuntimePlatform.WebGLPlayer && !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);
            hostButton.onClick.SetListener(() => { manager.StartHost(); });
            cancelButton.gameObject.SetActive(NetworkClient.isConnecting);
            cancelButton.onClick.SetListener(() => { manager.StopClient(); });
            dedicatedButton.interactable = Application.platform != RuntimePlatform.WebGLPlayer && !manager.isNetworkActive;
            dedicatedButton.onClick.SetListener(() => { manager.StartServer(); });
            quitButton.onClick.SetListener(() => { NetworkManagerMMO.Quit(); });

            // inputs
            auth.loginAccount = accountInput.text;
            auth.loginPassword = passwordInput.text;

            // copy servers to dropdown; copy selected one to networkmanager ip/port.
            serverDropdown.interactable = !manager.isNetworkActive;
            serverDropdown.options = manager.serverList.Select(
                sv => new Dropdown.OptionData(sv.name)
            ).ToList();
            manager.networkAddress = manager.serverList[serverDropdown.value].ip;
        }
        else panel.SetActive(false);
    }
    private void OnClipboardRead(string text) {  
        Debug.Log("Clipboard text: " + text);
    }
    private void OnClipboardError(string error) {
        Debug.LogError("Clipboard error: " + error);
    }
}
