// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.
using UnityEngine;
using UnityEngine.UI;

public partial class UIHeroNFT : MonoBehaviour
{
    public KeyCode hotKey = KeyCode.I;
    public GameObject panel;
    public Transform content;
    public Text goldText;
    public UIDragAndDropable trash;
    public Image trashImage;
    public GameObject trashOverlay;
    public Text trashAmountText;

    [Header("Durability Colors")]
    public Color brokenDurabilityColor = Color.red;
    public Color lowDurabilityColor = Color.magenta;
    [Range(0.01f, 0.99f)] public float lowDurabilityThreshold = 0.1f;

    void Update()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            // print(panel.activeSelf);
            // hotkey (not while typing in chat, etc.)
            if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
                panel.SetActive(!panel.activeSelf);

            // only update the panel if it's active
            if (panel.activeSelf)
            {
                ((PlayerEquipment)player.equipment).avatarCamera.enabled = panel.activeSelf;
                
            }
        }
        else panel.SetActive(false);
    }
}
