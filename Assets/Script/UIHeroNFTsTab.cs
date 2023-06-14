using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
public class UIHeroNFTsTab : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject nftManager;
    public TMP_Dropdown characterDropDown;
    public NetworkManagerMMO manager; 
    public List<string> list = new List<string>() {"test0 (to)", "test1 (t1)" };
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
        
    }
}
