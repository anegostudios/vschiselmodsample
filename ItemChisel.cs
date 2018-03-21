using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VSExampleMods
{
    /// <summary>
    /// When right clicked on a block, this chisel tool will exchange given block into a chiseledblock which 
    /// takes on the model of the block the player interacted with in the first place, but with each voxel being selectable and removable
    /// </summary>
    public class ItemChisel : Item
    {

        public override bool OnHeldAttackStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel);

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiselmod:chiseledblock"));

            if (block == chiseledblock)
            {
                IPlayer byPlayer = null;
                if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
                return OnBlockInteract(byEntity.World, byPlayer, blockSel, true);
            }

            return false;
        }

        public override bool OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel);
            
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);


            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiselmod:chiseledblock"));

            if (block == chiseledblock)
            {
                IPlayer byPlayer = null;
                if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
                return OnBlockInteract(byEntity.World, byPlayer, blockSel, false);
            }

            if (block.DrawType != Vintagestory.API.Client.EnumDrawType.Cube) return false;

            //if (block.FirstCodePart() != "rock") return false;


            byEntity.World.BlockAccessor.SetBlock(chiseledblock.BlockId, blockSel.Position);

            BlockEntityChisel be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be == null) return false;

            be.WasPlaced(block);
            return true;
        }

        public bool OnBlockInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool isBreak)
        {
            BlockEntityChisel bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bec != null)
            {
                bec.OnBlockInteract(byPlayer, blockSel, isBreak);
                return true;
            }

            return false;
        }




    }
}
