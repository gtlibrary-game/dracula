using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
public class UIMainMenu : MonoBehaviour
{
    public UICharacterCreation uiCharacterCreation;
    public UINFTManagement uiNFTManagement;
    public NetworkManagerMMO manager; // singleton is null until update
    public GameObject panel;
    
    public GameObject GameSettingPanel;
    public UICharacterSelection uiCharacterSelection;
    // public InputField nameInput;
    // public Dropdown classDropdown;
    public GameObject HeroSelectionTab;
    public Button GameSettingButton;
    public Button HeroNFTsButton;
    public GameObject HeroNFTsTab;
    public Button GameButton;
    public Button BookmarkButton;
    public Button MintButton;
    void Update()
    {
        
        // only update while visible (after character selection made it visible)

            
            // // still in lobby?
            if (manager.state == NetworkState.Lobby && !uiCharacterCreation.IsVisible() && !uiNFTManagement.IsVisible())
            {
                    Show();
                    GameSettingButton.onClick.SetListener(() => {
                        GameSettingPanel.SetActive(true);
                        Hide();
                    });

                    HeroNFTsButton.onClick.SetListener(() => {
                            HeroSelectionTab.SetActive(false);
                            HeroNFTsTab.SetActive(true);
                    });

                    GameButton.onClick.SetListener(() => {
                            HeroSelectionTab.SetActive(true);
                            HeroNFTsTab.SetActive(false);
                    });

                    BookmarkButton.onClick.SetListener(()=>{
                        // NetworkClient.Ready();
                          
                    });
                    BookmarkButton.onClick.SetListener(()=>{
                         // int druidValue = classValues[heroName];
                        // Contract contract = ThirdwebManager.Instance.SDK.GetContract(conttAddress,abihero);
                        // walletAddress = await ThirdwebManager.Instance.SDK.wallet.GetAddress();
                        // var nowTokenId = await getNextTokenId();
                        // var resultMint = await contract.Write("heroMint","1",walletAddress,"15","1000000000000000000");

                        // // string characterName = auth.manager.charactersAvailableMsg.characters[auth.manager.selection].name;
                        // // auth.manager.nowCharacterName = characterName;
                        // print(auth.manager.nowCharacterName);
                        // // print(auth.manager.selection);
                        // HeroMintNFTMsg message = new HeroMintNFTMsg{
                        //     playFabId=auth.playFabId,
                        //     sessionTicket=auth.sessionTicket,
                        //     signedTicket=auth.signedTicket,
                        //     nowCharacterName=auth.manager.nowCharacterName,
                        //     heroId = nowTokenId.ToString()
                        // }; //, signedTicket=signedTicket};
                        // // HeroMintNFTMsg message = new HeroMintNFTMsg{account=characterName, password="hash", version=Application.version};
                        // NetworkClient.connection.Send(message);
                        // Debug.Log("HeroMintNFTMsg message was sent");
                          
                    });
                }
            else Hide();
            
    }

    public void HeroNFTsTabShowHide() { 
        if(!HeroNFTsTab.activeSelf)
            HeroNFTsTab.SetActive(true); 
        else
            HeroNFTsTab.SetActive(false); 
    }
    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
    public bool IsVisible() { return panel.activeSelf; }
}
