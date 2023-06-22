using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
public class UIHeroNFTsTab : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject nftManager;
    public TMP_Dropdown characterDropDown;
    public NetworkManagerMMO manager;
    public TMP_InputField nameInput;
    public Button createHeroButton;
    void Start()
    {
        
    }

    void OnEnable(){

        // nftManager.GetComponent<NFTManager>().getBookmarkByWallet();
        
        nftManager.GetComponent<NFTManager>().IsConnectedWallet();
        handleCharacterDropdown();
        createHeroButton.onClick.RemoveAllListeners();
        createHeroButton.onClick.AddListener(async () =>
        {
            var message = new CharacterCreateMsg
            {
                name = nameInput.text,
                classIndex = characterDropDown.value,
                // heroTokenId = newTokenId,
                gameMaster = false
            };
            NetworkClient.Send(message);
            print("===============createHeroButton=============");
            // NFTManager nFTManagerCS = nftManager.GetComponent<NFTManager>();
            // string currentOptionText = characterDropDown.options[characterDropDown.value].text;
            // await nFTManagerCS.heroMint(currentOptionText);
            // try
            // {
                
            // }
            // catch (Exception ex)
            // {
            //     // Handle the error here
            // }
        });
    }

    public void handleCharacterDropdown(){

        characterDropDown.options.Clear();
        characterDropDown.options = manager.playerClasses.Select(
                                    p => new TMP_Dropdown.OptionData(p.name)
                                ).ToList();
    }
    // Update is called once per frame
    void Update()
    {
        NFTManager nFTManagerCS = nftManager.GetComponent<NFTManager>();
        createHeroButton.interactable = nFTManagerCS.walletFlg;
        
    }
}
