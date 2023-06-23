﻿using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
public partial class UINFTManagement : MonoBehaviour
{
    public NetworkManagerMMO manager; // singleton is null until update
    public GameObject panel;
    public InputField nameInput;
    public Dropdown classDropdown;
    public Toggle gameMasterToggle;
    public Button createButton;
    public Button cancelButton;
    public Button bcancelButton;
    public Button GetNFTButton;

    void Update()
    {
        // only update while visible (after character selection made it visible)
        if (panel.activeSelf)
        {
            // still in lobby?
            if (manager.state == NetworkState.Lobby)
            {
                Show();

                // copy player classes to class selection
                classDropdown.options = manager.playerClasses.Select(
                    p => new Dropdown.OptionData(p.name)
                ).ToList();

                // only show GameMaster option for host connection
                // -> this helps to test and create GameMasters more easily
                // -> use the database field for dedicated servers!
                gameMasterToggle.gameObject.SetActive(NetworkServer.activeHost);

                // create
                createButton.interactable = manager.IsAllowedCharacterName(nameInput.text);
                createButton.onClick.SetListener(() => {
                    CharacterCreateMsg message = new CharacterCreateMsg {
                        name = nameInput.text,
                        classIndex = classDropdown.value,
                        gameMaster = gameMasterToggle.isOn
                    };
                    NetworkClient.Send(message);
                    Hide();
                });
                bcancelButton.onClick.SetListener(() => {
                    nameInput.text = "";
                    Hide();
                });
                GetNFTButton.onClick.SetListener(() => {
                });
            }
            else Hide();
        }
    }

    public void test(string a, string b, string c)
    {

        print(a);
        print(b);
        print(c);

    }

    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
    public bool IsVisible() { return panel.activeSelf; }
}
