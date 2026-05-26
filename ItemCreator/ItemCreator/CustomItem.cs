using System.Linq;
using TMPro;
using UnityEngine;

namespace ItemCreator;

public class CustomItem(Sprite sprite, bool showCount, string languageKey, string playerDataInt)
{
    private Sprite _itemSprite = sprite;
    
    private GameObject _invItem;
    private SpriteRenderer _invItemRenderer;
    
    public void Register()
    {
        ItemCreator.Items.Add(this);
        if (GameCameras.instance) SetupItem();
    }

    public void Unregister()
    {
        ItemCreator.Items.Remove(this);
        if (_invItem) Object.Destroy(_invItem);
        
        var fg = GetFadeGroup();
        fg.spriteRenderers = fg.spriteRenderers.Where(i => i).ToArray();
        fg.texts = fg.texts.Where(i => i).ToArray();
    }

    public void SetupItem()
    {
        if (_invItem) return; 
        
        var eq = GameCameras.instance.hudCamera.transform.Find("Inventory").Find("Inv").Find("Equipment");
        _invItem = Object.Instantiate(eq.Find(showCount ? "Rancid Egg" : "Lantern").gameObject, eq);
        _invItem.name = languageKey;
        _invItem.transform.SetAsLastSibling();
        
        _invItemRenderer = _invItem.GetComponent<SpriteRenderer>();
        
        var fg = GetFadeGroup();
        fg.spriteRenderers = fg.spriteRenderers.Append(_invItemRenderer).ToArray();

        if (showCount)
        {
            _invItem.GetComponentInChildren<DisplayItemAmount>(true).playerDataInt = playerDataInt;
            fg.texts = fg.texts.Append(_invItem.GetComponentInChildren<TextMeshPro>(true)).ToArray();
        }
        
        _invItemRenderer.sprite = _itemSprite;
    }

    public void SetSprite(Sprite n)
    {
        _itemSprite = n;
        if (_invItemRenderer) _invItemRenderer.sprite = n;
    }

    public int GetAmount() => PlayerData.instance.GetInt(playerDataInt);

    public GameObject GetObject() => _invItem;

    private static FadeGroup _fadeGroup;
    
    private static FadeGroup GetFadeGroup()
    {
        return _fadeGroup ??= 
            GameCameras.instance.hudCamera.transform.Find("Inventory").Find("Inv").GetComponent<FadeGroup>();
    } 
}