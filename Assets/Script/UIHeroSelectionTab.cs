using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Thirdweb;
using Mirror;
public class UIHeroSelectionTab : MonoBehaviour
{
    public GameObject panel;
    public GameObject networkManagerObj;
    Transform didSelect = null;
    Transform selecting = null;
    // Start is called before the first frame update
    Transform selected = null;
    public GameObject characterName;
    public Button MintButton;
    public TextMeshProUGUI nameInput;
    public string currentCharacterName = null;
    void Start()
    {
        
    }
    void OnEnable(){
        selecting = networkManagerObj.transform.GetChild(1);
        int index = selecting.GetSiblingIndex();
        DisplayCharacter(selecting);
        // mintButton.interactable = manager.IsAllowedCharacterName(nameInput.text);
        // MintButton.onClick.SetListener(async () => {
        //     Contract contract = ThirdwebManager.Instance.SDK.GetContract(nftManager.conttAddress,nftManager.abihero);
        //     var walletAddress = await ThirdwebManager.Instance.SDK.wallet.GetAddress();
        //     var nowTokenId = await nftManager.getNextTokenId();
        //     var resultMint = await contract.Write("heroMint","1",walletAddress,"15","1000000000000000000");

        //     // string characterName = auth.manager.charactersAvailableMsg.characters[auth.manager.selection].name;
        //     // auth.manager.nowCharacterName = characterName;
        //     print(currentCharacterName);
        //     // print(auth.manager.selection);
        //     HeroMintNFTMsg message = new HeroMintNFTMsg{
        //         playFabId=auth.playFabId,
        //         sessionTicket=auth.sessionTicket,
        //         signedTicket=auth.signedTicket,
        //         nowCharacterName=currentCharacterName,
        //         heroId = nowTokenId.ToString()
        //     }; //, signedTicket=signedTicket};
        //     // HeroMintNFTMsg message = new HeroMintNFTMsg{account=characterName, password="hash", version=Application.version};
        //     NetworkClient.connection.Send(message);
        //     Debug.Log("HeroMintNFTMsg message was sent");
        // });
    }

    void DisplayCharacter(Transform trnsobj){
        if(trnsobj.childCount>0){

            trnsobj.GetChild(0).GetComponent<SelectableCharacter>().OnMouseDown();
            trnsobj.GetChild(0).GetComponent<PlayerEquipment>().avatarCamera.enabled = true;
            selecting = trnsobj;
            characterName.GetComponent<TMP_InputField>().text = selecting.GetChild(0).name;
            currentCharacterName = selecting.GetChild(0).name;
            if(selected != null)
            selected.GetChild(0).GetComponent<PlayerEquipment>().avatarCamera.enabled = false;
        }
    }

    public void nextCharacter(){
        int index = selecting.GetSiblingIndex();
        selected = selecting;
        Transform nextBrotherNode = networkManagerObj.transform.GetChild(index + 1);
        DisplayCharacter(nextBrotherNode);
    }
    public void prevCharacter(){
        int index = selecting.GetSiblingIndex();
        selected = selecting;
        Transform nextBrotherNode = networkManagerObj.transform.GetChild(index - 1);
        DisplayCharacter(nextBrotherNode);
    }
    // Update is called once per frame
    void Update()
    {
       
    }

    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
    public bool IsVisible() { return panel.activeSelf; }
}
