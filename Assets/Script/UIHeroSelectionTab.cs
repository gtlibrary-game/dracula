using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class UIHeroSelectionTab : MonoBehaviour
{
    public GameObject panel;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void OnEnable(){

    }
    // Update is called once per frame
    void Update()
    {
        
    }

    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
    public bool IsVisible() { return panel.activeSelf; }
}
