using System;
using Knitting.Items;
using Vintagestory.API.Common;

namespace Knitting;

public class KnittingModSystem : ModSystem
{
    public static KnittingConfig config;

    public override void StartPre(ICoreAPI api)
    {
        api.RegisterItemClass("ItemKnittingNeedles", typeof(ItemKnittingNeedles));
    }

    public override void Start(ICoreAPI api)
    {
        try
        {
            config = api.LoadModConfig<KnittingConfig>("KnittingConfig.json") ?? new KnittingConfig();
            api.StoreModConfig<KnittingConfig>(config, "KnittingConfig.json");
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Error loading Knitting config. Using default settings instead.");
            Mod.Logger.Error(e);
            config = new KnittingConfig();
        }
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        ItemKnittingNeedles.MapOutputs(api);
    } 
}
