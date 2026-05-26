using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Satchel;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ItemCreator;

public static class ItemUtils
{
    private static Action<PlayMakerFSM> _onFsmAwake;

    public class ItemCreatorManager : MonoBehaviour;

    private static ItemCreatorManager _icm;
    
    internal static void Init()
    {
        var icm = new GameObject("[ItemCreator] Manager");
        Object.DontDestroyOnLoad(icm);
        _icm = icm.AddComponent<ItemCreatorManager>();
        
        var backboard = LoadSpriteResource("trinket_backboard");

        On.PlayMakerFSM.Awake += (orig, self) =>
        {
            orig(self);
            _onFsmAwake(self);
        };
        
        // Ensures that the order of vanilla objects is the same as vanilla
        _onFsmAwake += fsm =>
        {
            if (fsm.FsmName != "Build Equipment List") return;

            var ci = fsm.FsmVariables.FindFsmGameObject("Current Item");
            
            foreach (var state in fsm.FsmStates)
            {
                if (state.Name.StartsWith("Trink ")) continue;
                var fc = state.GetFirstActionOfType<FindChild>();
                if (fc == null) continue;
                var i = state.Actions.ToList().IndexOf(fc) + 1;
                state.InsertCustomAction(() =>
                {
                    if (ci.Value) ci.Value.transform.SetAsLastSibling();
                }, i);
            }

            var nextNum = fsm.FsmVariables.FindFsmInt("Next Item Num");
            fsm.GetValidState("All Done :)").InsertCustomAction(() =>
            {
                foreach (var item in ItemCreator.Items)
                {
                    item.SetupItem();
                    if (item.GetAmount() > 0)
                    {
                        nextNum.Value++;
                        var o = item.GetObject();
                        o.SetActive(true);
                        o.transform.SetAsLastSibling();
                    } else item.GetObject().SetActive(false);
                }
            }, 0);
            
            fsm.GetValidState("Trink 1").DisableActions(3, 5, 6);
            fsm.GetValidState("Trink 2").DisableActions(3, 5, 6);
            fsm.GetValidState("Trink 3").DisableActions(3, 5, 6);
            fsm.GetValidState("Trink 4").DisableActions(0, 4, 6, 7);
            
            var tb = fsm.transform.parent.Find("trinket_backboard");
            var irb = tb.GetComponent<InvRelicBackboard>();
            var sr = irb.GetComponent<SpriteRenderer>();
            if (sr.sprite == irb.activeSprite) sr.sprite = backboard;
            irb.activeSprite = backboard;
            
            tb.SetLocalPositionY(-12.65f);
            fsm.transform.Find("Trinket1").SetLocalPositionY(-12.57f);
            fsm.transform.Find("Trinket2").SetLocalPositionY(-12.57f);
            fsm.transform.Find("Trinket3").SetLocalPositionY(-12.57f);
            fsm.transform.Find("Trinket4").SetLocalPositionY(-12.57f);
        };

        // Modify equipment position calculations
        _onFsmAwake += fsm =>
        {
            if (fsm.FsmName != "equip_position") return;

            fsm.GetValidState("Loop Check").DisableAction(0);
            var sp = fsm.GetValidState("Set Position");
            sp.DisableAction(0);
            sp.AddCustomAction(() =>
            {
                var count = GetEnabledIndex(fsm.transform);

                DoSetPositionByIndex(fsm.transform, count);
            });
        };
        
        _onFsmAwake += fsm =>
        {
            if (fsm.FsmName != "UI Inventory") return;

            var uc = fsm.gameObject.LocateMyFSM("Update Cursor");
            var item = uc.FsmVariables.FindFsmGameObject("Item");

            var ut = fsm.gameObject.LocateMyFSM("Update Text");
            var convoName = ut.FsmVariables.FindFsmString("Convo Name");
            var convoDesc = ut.FsmVariables.FindFsmString("Convo Desc");

            var bc = fsm.gameObject.LocateMyFSM("Button Control");
            var itemName = bc.FsmVariables.FindFsmString("Current Item Name");

            var lastDir = Dir.None;

            var fromRight = false;
            fsm.GetValidState("To Equip").InsertCustomAction(() => fromRight = false, 0);
            fsm.GetValidState("To Equip 2").InsertCustomAction(() => fromRight = false, 0);
            fsm.GetValidState("R Arrow").InsertCustomAction(() => fromRight = true, 0);

            var fil = fsm.AddState("Focus Item L");
            var fir = fsm.AddState("Focus Item R");
            var fiu = fsm.AddState("Focus Item U");
            var fid = fsm.AddState("Focus Item D");

            var cursorUpdateId = 0;
            var fi = fsm.AddState("Focus Item");
            fi.AddCustomAction(() =>
            {
                var ld = lastDir;
                lastDir = Dir.None;
                if (item.Value && item.Value.transform.parent.name == "Equipment")
                {
                    if (item.Value.name.StartsWith("Trinket")) ld = Dir.Up;
                    // Move to next based on last dir
                    var found = false;
                    switch (ld)
                    {
                        case Dir.Left:
                        case Dir.Right:
                            // Find enabled to the left/right (same y val)
                            var lrn = FindEquipmentLeftRight(item.Value.transform, ld == Dir.Left);
                            if (lrn)
                            {
                                found = true;
                                item.Value = lrn;
                            }
                            break;
                        case Dir.Up:
                        case Dir.Down:
                            // Find enabled above/below (same x val)
                            var udn = FindEquipmentAboveBelow(item.Value.transform, ld == Dir.Up);
                            if (udn)
                            {
                                found = true;
                                item.Value = udn;
                            }
                            break;
                        case Dir.None:
                            found = true;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    // If no valid equipment found in direction
                    if (!found) {
                        switch (ld)
                        {
                            case Dir.Left:
                                fsm.SetState("Choice 16");
                                break;
                            case Dir.Right:
                                fsm.SetState("Other Panes? R");
                                break;
                            case Dir.Down:
                                var trinket = FindEquipmentAboveBelow(item.Value.transform, false, true);
                                if (trinket)
                                {
                                    fsm.SetState($"Trinket {trinket.name.Last()}");
                                }
                                break;
                            case Dir.Up:
                            case Dir.None:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        return;
                    }
                }
                else
                {
                    var eq = FindEquipment(fsm.transform.Find("Equipment"), fromRight);
                    if (!eq)
                    {
                        fsm.SetState(fromRight ? "Choice 16" : "Other Panes? R");
                        return;
                    }
                    item.Value = eq;
                }
                
                var index = GetEnabledIndex(item.Value.transform);
                var activeCount = item.Value.transform.parent.Cast<Transform>()
                    .Count(o => o.gameObject.activeInHierarchy && !o.name.StartsWith("Trink"));

                var offset = 0;
                if (index >= 8 && activeCount > 16)
                {
                    offset += Mathf.FloorToInt((index - 8) / 4f) + 1;
                    
                    if (Mathf.FloorToInt(index / 4f) == Mathf.FloorToInt(activeCount / 4f)
                        || (Mathf.FloorToInt(index / 4f) == Mathf.FloorToInt(activeCount / 4f) - 1 
                            && activeCount % 4 == 0)) offset -= 2;
                    else if (Mathf.FloorToInt(index / 4f) == Mathf.FloorToInt(activeCount / 4f) - 1 
                             || (Mathf.FloorToInt(index / 4f) == Mathf.FloorToInt(activeCount / 4f) - 2
                            && activeCount % 4 == 0)) offset--;
                }

                var c = 0;
                var skip = true;
                foreach (var i in item.Value.transform.parent.Cast<Transform>()
                             .Where(i => i.gameObject.activeInHierarchy && !i.name.StartsWith("Trink")))
                {
                    skip &= DoSetPositionByIndex(i, c, offset, 0.15f);
                    c++;
                }
                
                var id = GetIdFor(item.Value.name);
                convoName.Value = itemName.Value = $"INV_NAME_{id}";
                convoDesc.Value = $"INV_DESC_{id}";

                cursorUpdateId++;
                var cacheId = cursorUpdateId;
                if (skip) uc.SendEvent("UPDATE CURSOR");
                else fsm.StartCoroutine(UpdateCursorLater());
                
                return;

                IEnumerator UpdateCursorLater()
                {
                    yield return new WaitForSeconds(0.15f);
                    if (cursorUpdateId != cacheId) yield break;
                    uc.SendEvent("UPDATE CURSOR");
                }
            });
            
            fi.AddTransition("UI LEFT", fil.Name);
            fi.AddTransition("UI RIGHT", fir.Name);
            fi.AddTransition("UI UP", fiu.Name);
            fi.AddTransition("UI DOWN", fid.Name);
            
            fil.AddCustomAction(() => lastDir = Dir.Left);
            fir.AddCustomAction(() => lastDir = Dir.Right);
            fiu.AddCustomAction(() => lastDir = Dir.Up);
            fid.AddCustomAction(() => lastDir = Dir.Down);

            fil.AddTransition("FINISHED", fi.Name);
            fir.AddTransition("FINISHED", fi.Name);
            fid.AddTransition("FINISHED", fi.Name);
            fiu.AddTransition("FINISHED", fi.Name);
            
            foreach (var state in fsm.FsmStates)
            {
                if (state.Name.StartsWith("Equip Item "))
                {
                    foreach (var action in state.Actions) action.Enabled = false;
                    state.InsertCustomAction(() => fsm.SetState(fi.Name), 0);
                }
            }
        };
    }

    // Find the top leftmost or rightmost equipment
    private static GameObject FindEquipment(Transform equipment, bool fromRight)
    {
        Transform found = null;
        foreach (Transform child in equipment)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.name.StartsWith("Trinket")) continue;
            
            if (found)
            {
                if ((child.localPosition.x > found.localPosition.x == fromRight
                    && Mathf.Abs(child.localPosition.x - found.localPosition.x) > 0.2f) || 
                    (Mathf.Abs(child.localPosition.x - found.localPosition.x) < 0.2f 
                    && child.localPosition.y > found.localPosition.y)) found = child;
            }
            else found = child;
        }

        return found ? found.gameObject : null;
    }

    private static GameObject FindEquipmentAboveBelow(Transform current, bool above, bool trinkets = false)
    {
        Transform found = null;
        foreach (Transform child in current.parent)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.name.StartsWith("Trinket") != trinkets) continue;
            
            if (Mathf.Abs(child.localPosition.x - current.localPosition.x) > 0.2f) continue;
            if (Mathf.Abs(child.localPosition.y - current.localPosition.y) < 0.2f) continue;
            if (child.localPosition.y < current.localPosition.y == above) continue;
            
            if (found)
            {
                if (child.localPosition.y < found.localPosition.y == above) found = child;
            }
            else found = child;
        }

        return found ? found.gameObject : null;
    }

    private static GameObject FindEquipmentLeftRight(Transform current, bool left)
    {
        Transform found = null;
        foreach (Transform child in current.parent)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.name.StartsWith("Trinket")) continue;
            
            if (Mathf.Abs(child.localPosition.y - current.localPosition.y) > 0.2f) continue;
            if (Mathf.Abs(child.localPosition.x - current.localPosition.x) < 0.2f) continue;
            if (child.localPosition.x > current.localPosition.x == left) continue;
            
            if (found)
            {
                if (child.localPosition.x > found.localPosition.x == left) found = child;
            }
            else found = child;
        }

        return found ? found.gameObject : null;
    }

    private static string GetIdFor(string objName)
    {
        return objName switch
        {
            "Dash Cloak" => PlayerData.instance.GetBool("hasShadowDash") ? "SHADOWDASH" : "DASH",
            "Mantis Claw" => "WALLJUMP",
            "Super Dash" => "SUPERDASH",
            "Lantern" => "LANTERN",
            "Double Jump" => "DOUBLEJUMP",
            "Acid Armour" => "ACIDARMOUR",
            "Store Key" => "STOREKEY",
            "White Key" => "WHITEKEY",
            "Xun Flower" => PlayerData.instance.GetBool("xunFlowerBroken") ? "FLOWER_BROKEN" : "FLOWER" +
                (PlayerData.instance.GetBool("extraFlowerAppear") ? "_QG" : ""),
            "Tram Pass" => "TRAM_PASS",
            "Waterway Key" => "WATERWAYSKEY",
            "Ore" => "ORE",
            "City Key" => "CITYKEY",
            "Love Key" => "LOVEKEY",
            "Kings Brand" => "KINGSBRAND",
            "Rancid Egg" => "RANCIDEGG",
            "Simple Key" => "SIMPLEKEY",
            "Map and Quill" => PlayerData.instance.GetBool("hasMap") ? "MAP" :
                "" + (PlayerData.instance.GetBool("hasQuill") ? "QUILL" : ""),
            _ => objName
        };
    }

    private static int GetEnabledIndex(Transform trans)
    {
        return trans.parent.Cast<Transform>()
            .TakeWhile(child => child != trans)
            .Count(child => child.gameObject.activeInHierarchy && !child.name.StartsWith("Trink"));
    }

    private static bool DoSetPositionByIndex(Transform trans, int count, int offset = 0, float time = 0)
    {
        var target = new Vector3(
            1.1f + 2.08f * (count % 4), 
            -3.8f + -2.18f * (Mathf.Floor(count / 4f) - offset));
        if ((trans.localPosition - target).magnitude < 0.1f || time == 0)
        {
            trans.localPosition = target;
            return true;
        }
        _icm.StartCoroutine(MoveTo(trans, target, time));
        return false;
    }

    private static IEnumerator MoveTo(Transform trans, Vector3 target, float time)
    {
        var end = Time.time + time;
        var start = trans.localPosition;
        while (Time.time < end)
        {
            yield return null;
            trans.localPosition = Vector3.Lerp(target, start, Mathf.Clamp01((end - Time.time) / time));
        }
        
        trans.localPosition = target;
    }

    private enum Dir
    {
        Up,
        Down,
        Left,
        Right,
        None
    }

    private static void SetLocalPositionY(this Transform t, float newY)
    {
        var pos = t.localPosition;
        pos.y = newY;
        t.localPosition = pos;
    }

    private static void DisableActions(this FsmState state, params int[] actions)
    {
        foreach (var a in actions) state.DisableAction(a);
    }
    
    private static Sprite LoadSpriteResource(string spritePath)
    {
        var path = $"ItemCreator.{spritePath}.png";

        var asm = Assembly.GetExecutingAssembly();

        using var s = asm.GetManifestResourceStream(path);
        if (s == null) return null;
        var buffer = new byte[s.Length];
        _ = s.Read(buffer, 0, buffer.Length);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(buffer, true);
        tex.wrapMode = TextureWrapMode.Clamp;

        var sprite = Sprite.Create(
            tex, 
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 
            100, 
            0U, 
            SpriteMeshType.Tight);
        
        sprite.texture.filterMode = FilterMode.Bilinear;
        
        return sprite;
    }
}