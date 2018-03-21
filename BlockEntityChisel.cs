using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VSExampleMods
{
    public class BlockEntityChisel : BlockEntity, IBlockShapeSupplier
    {
        Block block;
        bool[,,] Voxels = new bool[16, 16, 16];
        MeshData mesh;
        Cuboidf[] selectionBoxes = new Cuboidf[0];

        

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (block != null)
            {
                if (api.Side == EnumAppSide.Client) RegenMesh();
                RegenSelectionBoxes();
            }

            
        }

        internal void WasPlaced(Block block)
        {
            this.block = block;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        Voxels[x, y, z] = true;
                    }
                }
            }

            if (api.Side == EnumAppSide.Client && mesh == null)
            {
                RegenMesh();
            }

            RegenSelectionBoxes();
        }

        internal void OnBlockInteract(IPlayer byPlayer, BlockSelection blockSel, bool isBreak)
        {
            if (api.World.Side == EnumAppSide.Client)
            {
                Cuboidf box = selectionBoxes[blockSel.SelectionBoxIndex];
                Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

                UpdateVoxel(byPlayer, voxelPos, blockSel.Face, isBreak);
            }
        }


        internal void UpdateVoxel(IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool isBreak)
        {
            Vec3i addAtPos = voxelPos.Clone().Add(facing);
            if (!isBreak)
            {
                if (addAtPos.X >= 0 && addAtPos.X < 16 && addAtPos.Y >= 0 && addAtPos.Y < 16 && addAtPos.Z >= 0 && addAtPos.Z < 16)
                {
                    Voxels[addAtPos.X, addAtPos.Y, addAtPos.Z] = true;
                }
            } else
            {
                Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] = false;
            }


            

            if (api.Side == EnumAppSide.Client)
            {
                RegenMesh();
            }

            RegenSelectionBoxes();
            MarkDirty(true);

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, isBreak);
            }
        }
        



        public void SendUseOverPacket(IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool isBreak)
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                writer.Write(voxelPos.X);
                writer.Write(voxelPos.Y);
                writer.Write(voxelPos.Z);
                writer.Write(isBreak);
                writer.Write((ushort)facing.Index);
                data = ms.ToArray();
            }

            ((ICoreClientAPI)api).Network.SendBlockEntityPacket(
                pos.X, pos.Y, pos.Z,
                (int)1000,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == 1000)
            {
                Vec3i voxelPos;
                bool isBreak;
                BlockFacing facing;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    voxelPos = new Vec3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    isBreak = reader.ReadBoolean();
                    facing = BlockFacing.ALLFACES[reader.ReadInt16()];
                }

                UpdateVoxel(player, voxelPos, facing, isBreak);
            }
        }



        internal Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            block = worldAccessForResolve.GetBlock((ushort)tree.GetInt("blockid"));
            deserializeVoxels(tree.GetBytes("voxels"));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("blockid", block.BlockId);
            tree.SetBytes("voxels", serializeVoxels());
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            //ICoreClientAPI capi = api as ICoreClientAPI;
            if (mesh == null) return false;

            mesher.AddMeshData(mesh);
            return true;
        }






        public void RegenSelectionBoxes()
        {
            List<Cuboidf> boxes = new List<Cuboidf>();

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (Voxels[x, y, z])
                        {
                            boxes.Add(new Cuboidf(x / 16f, y / 16f, z / 16f, x / 16f + 1 / 16f, y / 16f + 1 / 16f, z / 16f + 1 / 16f));
                        }
                    }
                }
            }

            selectionBoxes = boxes.ToArray();
        }


        public void RegenMesh()
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            mesh = new MeshData(24, 36, false).WithTints().WithRenderpasses();


            float subPixelPadding = capi.BlockTextureAtlas.SubPixelPadding;

            MeshData[] meshesByFace = new MeshData[6];

            

            // North
            meshesByFace[0] = QuadMeshUtil.GetCustomQuad(0, 0, 0, 1 / 16f, 1 / 16f, 255, 255, 255, 255);
            meshesByFace[0].Rotate(new Vec3f(1 / 32f, 1 / 32f, 1 / 32f), 0, GameMath.PI, 0);
            meshesByFace[0].Translate(0, 0, -1 / 16f);

            // East
            meshesByFace[1] = QuadMeshUtil.GetCustomQuad(0, 0, 0, 1 / 16f, 1 / 16f, 255, 255, 255, 255);
            meshesByFace[1].Rotate(new Vec3f(1 / 32f, 1 / 32f, 1 / 32f), 0, GameMath.PIHALF, 0);
            meshesByFace[1].Translate(1 / 16f, 0, 0);

            // South
            meshesByFace[2] = QuadMeshUtil.GetCustomQuad(0, 0, 1/ 16f, 1 / 16f, 1 / 16f, 255, 255, 255, 255);

            // West
            meshesByFace[3] = QuadMeshUtil.GetCustomQuad(0, 0, 0, 1 / 16f, 1 / 16f, 255, 255, 255, 255);
            meshesByFace[3].Rotate(new Vec3f(1 / 32f, 1 / 32f, 1 / 32f), 0, -GameMath.PIHALF, 0);
            meshesByFace[3].Translate(-1 / 16f, 0, 0);

            // Up
            meshesByFace[4] = QuadMeshUtil.GetCustomQuadHorizontal(0, 1 / 16f, 0, 1 / 16f, 1 / 16f, 255, 255, 255, 255);
            meshesByFace[4].Rotate(new Vec3f(1 / 32f, 1 / 32f, 1 / 32f), GameMath.PI, 0, 0);
            meshesByFace[4].Translate(0, 1/16f, 0);

            // Down
            meshesByFace[5] = QuadMeshUtil.GetCustomQuadHorizontal(0, 0, 0, 1 / 16f, 1 / 16f, 255, 255, 255, 255);


            float[] sideShadings = CubeMeshUtil.DefaultBlockSideShadingsByFacing;

            for (int i = 0; i < meshesByFace.Length; i++)
            {
                MeshData mesh = meshesByFace[i];
                mesh.Rgba = new byte[16];
                mesh.Rgba.Fill((byte)(255 * sideShadings[i]));

                mesh.rgba2 = new byte[16];
                mesh.rgba2.Fill((byte)(255 * sideShadings[i]));
                mesh.Flags = new int[4];
                mesh.Flags.Fill(0);
                mesh.RenderPasses = new int[1];
                mesh.RenderPassCount = 1;
                mesh.Tints = new int[1];
                mesh.TintsCount = 1;
                mesh.XyzFaces = new int[] { i };
                mesh.XyzFacesCount = 1;

                TextureAtlasPosition tpos = capi.BlockTextureAtlas.GetPosition(block, BlockFacing.ALLFACES[i].Code);
                for (int j = 0; j < mesh.Uv.Length; j++)
                {
                    mesh.Uv[j] = (j % 2 > 0 ? tpos.y1 : tpos.x1) + mesh.Uv[j] * 32f / capi.BlockTextureAtlas.Size - subPixelPadding;
                }
            }

            MeshData[] voxelMeshesOffset = new MeshData[6];
            for (int i = 0; i < meshesByFace.Length; i++)
            {
                voxelMeshesOffset[i] = meshesByFace[i].Clone();
            }
            

            // North: Negative Z 
            // East: Positive X 
            // South: Positive Z 
            // West: Negative X

            bool[] sideVisible = new bool[6];

            int[] coords = new int[3];

            int[][] coordIndexByFace = new int[][] {
                // N
                new int[] { 0, 1 },
                // E
                new int[] { 2, 1 },
                // S
                new int[] { 0, 1 },
                // W
                new int[] { 2, 1 },
                // U
                new int[] { 0, 2 },
                // D
                new int[] { 0, 2 }
            };

            for (int x = 0; x < 16; x++)
            {
                coords[0] = x;

                for (int y = 0; y < 16; y++)
                {
                    coords[1] = y;

                    for (int z = 0; z < 16; z++)
                    {
                        if (!Voxels[x, y, z]) continue;

                        coords[2] = z;

                        float px = x / 16f;
                        float py = y / 16f;
                        float pz = z / 16f;

                        sideVisible[0] = z == 0 || !Voxels[x, y, z - 1];
                        sideVisible[1] = x == 15 || !Voxels[x + 1, y, z];
                        sideVisible[2] = z == 15 || !Voxels[x, y, z+1];
                        sideVisible[3] = x == 0 || !Voxels[x - 1, y, z];
                        sideVisible[4] = y == 15 || !Voxels[x, y + 1, z];
                        sideVisible[5] = y == 0 || !Voxels[x, y - 1, z];

                        for (int f = 0; f < 6; f++)
                        {
                            if (!sideVisible[f]) continue;

                            MeshData facerefmesh = meshesByFace[f];
                            MeshData faceoffsetmesh = voxelMeshesOffset[f];

                            for (int i = 0; i < facerefmesh.xyz.Length; i += 3)
                            {
                                faceoffsetmesh.xyz[i] = px + facerefmesh.xyz[i];
                                faceoffsetmesh.xyz[i + 1] = py + facerefmesh.xyz[i + 1];
                                faceoffsetmesh.xyz[i + 2] = pz + facerefmesh.xyz[i + 2];
                            }

                            float offsetX = (coords[coordIndexByFace[f][0]] * 2f) / capi.BlockTextureAtlas.Size;
                            float offsetZ = (coords[coordIndexByFace[f][1]] * 2f) / capi.BlockTextureAtlas.Size;

                            for (int i = 0; i < facerefmesh.Uv.Length; i += 2)
                            {
                                faceoffsetmesh.Uv[i] = facerefmesh.Uv[i] + offsetX;
                                faceoffsetmesh.Uv[i + 1] = facerefmesh.Uv[i + 1] + offsetZ;
                            }

                            mesh.AddMeshData(faceoffsetmesh);
                        }

                    }
                }
            }
        }










        byte[] serializeVoxels()
        {
            byte[] data = new byte[16 * 16 * 16 / 8];
            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = pos % 8;
                        data[pos / 8] |= (byte)((Voxels[x, y, z] ? 1 : 0) << bitpos);
                        pos++;
                    }
                }
            }

            return data;
        }

        void deserializeVoxels(byte[] data)
        {
            Voxels = new bool[16, 16, 16];

            if (data == null || data.Length < 16 * 16 * 16 / 8) return;

            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = pos % 8;
                        Voxels[x, y, z] = (data[pos / 8] & (1 << bitpos)) > 0;
                        pos++;
                    }
                }
            }
        }
    }
}
