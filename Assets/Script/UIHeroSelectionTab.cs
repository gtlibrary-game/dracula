using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class UIHeroSelectionTab : MonoBehaviour
{
    public GameObject panel;
    public GameObject networkManagerObj;
    Transform didSelect = null;
    Transform selecting = null;
    // Start is called before the first frame update
    Transform selected = null;
    public GameObject characterName;
    void Start()
    {
        
    }
    void OnEnable(){
        selecting = networkManagerObj.transform.GetChild(1);
        int index = selecting.GetSiblingIndex();
        DisplayCharacter(selecting);
    }

    void DisplayCharacter(Transform trnsobj){
        if(trnsobj.childCount>0){

            trnsobj.GetChild(0).GetComponent<SelectableCharacter>().OnMouseDown();
            trnsobj.GetChild(0).GetComponent<PlayerEquipment>().avatarCamera.enabled = true;
            selecting = trnsobj;
            characterName.GetComponent<TMP_InputField>().text = selecting.GetChild(0).name;
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
