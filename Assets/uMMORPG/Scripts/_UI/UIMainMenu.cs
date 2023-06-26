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
