using Knitting.Items;
using Vintagestory.API.Common;

namespace Knitting;

public class KnittingModSystem : ModSystem
{
    public override void StartPre(ICoreAPI api)
    {
        api.RegisterItemClass("ItemKnittingNeedles", typeof(ItemKnittingNeedles));
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        ItemKnittingNeedles.MapOutputs(api);
    } 
}
