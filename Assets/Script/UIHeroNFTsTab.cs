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

        nftManager.GetComponent<NFTManager>().getBookmarkByWallet();

        handleCharacterDropdown();
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
        createHeroButton.onClick.SetListener(() => {
            CharacterCreateMsg message = new CharacterCreateMsg {
                name = nameInput.text,
                classIndex = characterDropDown.value,
                gameMaster = false
            };
            NetworkClient.Send(message);
        });
    }
}
