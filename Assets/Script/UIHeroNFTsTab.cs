using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class UIHeroNFTsTab : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject nftManager;
    void Start()
    {
        
    }

    void OnEnable(){

        nftManager.GetComponent<NFTManager>().onWalletConnected();
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
