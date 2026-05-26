using System.Collections.Generic;
using Modding;
using System.Reflection;

namespace ItemCreator;

internal class ItemCreator() : Mod("ItemCreator")
{
    internal static readonly List<CustomItem> Items = [];
    
    public override string GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }

    public override void Initialize()
    {
        Log("Initializing");
        ItemUtils.Init();
        Log("Initialized");
    }
}