using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIMainMenu : MonoBehaviour
{
    public UICharacterCreation uiCharacterCreation;
    public UINFTManagement uiNFTManagement;
    public NetworkManagerMMO manager; // singleton is null until update
    public GameObject panel;
    public GameObject HeroNFTsTab;
    public GameObject GameSettingPanel;
    public UICharacterSelection uiCharacterSelection;
    // public InputField nameInput;
    // public Dropdown classDropdown;
    public Button GameSettingButton;

    void Update()
    {
        
        // only update while visible (after character selection made it visible)

            
            // // still in lobby?
            if (manager.state == NetworkState.Lobby && !uiCharacterCreation.IsVisible() && !uiNFTManagement.IsVisible() && !uiCharacterSelection.IsVisible())
            {
                    Show();
                    GameSettingButton.onClick.SetListener(() => {
                        GameSettingPanel.SetActive(true);
                        Hide();
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
