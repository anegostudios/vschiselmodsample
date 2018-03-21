using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VSExampleMods
{
    public class ChiselMod : ModBase
    {

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemChisel", typeof(ItemChisel));
            api.RegisterBlockClass("BlockChisel", typeof(BockChisel));
            api.RegisterBlockEntityClass("Chisel", typeof(BlockEntityChisel));
        }

    }
}
