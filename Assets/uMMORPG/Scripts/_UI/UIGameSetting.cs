using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIGameSetting : MonoBehaviour
{
    public UICharacterCreation uiCharacterCreation;
    public UINFTManagement uiNFTManagement;
    public NetworkManagerMMO manager; // singleton is null until update
    public GameObject panel;
    public GameObject GameSettingPanel;
    // public InputField nameInput;
    // public Dropdown classDropdown;
    public Button GameSettingButton;

    void Update()
    {
        
        // only update while visible (after character selection made it visible)

            
         
            
    }

    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
    public bool IsVisible() { return panel.activeSelf; }
}
