﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ADTTex
{
    public void ReadMVER(MemoryStream ADTstream)
    {
        ADTstream.Position += 4;
    }

    public void ReadMAMP (MemoryStream ADTtexstream)
    {
        StreamTools s = new StreamTools();
        int texture_size = s.ReadLong(ADTtexstream); // either defined here or in MHDR.mamp_value.
    }

    public void ReadMTEX (MemoryStream ADTtexstream, int MTEXsize)
    {
        if (ADTtexstream.Length == ADTtexstream.Position)
            return;

        // texture path strings, separated by 0
        string texturePath = "";
        int numberOfTextures = 0;
        for (int a = 0; a < MTEXsize; a++)
	    {
            int b = ADTtexstream.ReadByte();
            if (b != 0)
            {
                var stringSymbol = System.Convert.ToChar(b);
                texturePath = texturePath + stringSymbol;
            }
            else if (b == 0)
            {
                ADTTexData.textureBlockData.terrainTexturePaths.Add(texturePath);
                string extractedPath = Casc.GetFile(texturePath);
                using (Stream stream = File.Open(extractedPath, FileMode.Open))
                {
                    BLP blp = new BLP();
                    byte[] data = blp.GetUncompressed(stream, true);
                    BLPinfo info = blp.Info();
                    ADTTexData.Texture2Ddata texture2Ddata = new ADTTexData.Texture2Ddata();
                    texture2Ddata.hasMipmaps = info.hasMipmaps;
                    texture2Ddata.width = info.width;
                    texture2Ddata.height = info.height;
                    if (info.width != info.height) // Unity doesn't support nonsquare mipmaps // sigh
                        texture2Ddata.hasMipmaps = false;
                    texture2Ddata.textureFormat = info.textureFormat;
                    texture2Ddata.TextureData = data;
                    ADTTexData.textureBlockData.terrainTextures.Add(texturePath, texture2Ddata);
                    texturePath = null;
                    numberOfTextures++;
                }
            }
        }
    }

    public void ReadMCNKtex (MemoryStream ADTtexstream, string mapname, int MCNKchunkNumber, int MCNKsize)
    {
        if (ADTtexstream.Length == ADTtexstream.Position)
            return;

        StreamTools s = new StreamTools();
        ADTTexData.TextureChunkData chunkData = new ADTTexData.TextureChunkData();
        
        long MCNKchnkPos = ADTtexstream.Position;
        long streamPosition = ADTtexstream.Position;
        while (streamPosition < MCNKchnkPos+MCNKsize)
        {
            ADTtexstream.Position = streamPosition;
            int chunkID = s.ReadLong(ADTtexstream);
            int chunkSize = s.ReadLong(ADTtexstream);
            streamPosition = ADTtexstream.Position + chunkSize;
            switch (chunkID)
            {
                case (int)ChunkID.ADT.MCLY:
                    ReadMCLY(ADTtexstream, chunkData, chunkSize); // texture layers
                    break;
                case (int)ChunkID.ADT.MCSH:
                    ReadMCSH(ADTtexstream, chunkData); // static shadow maps
                    break;
                case (int)ChunkID.ADT.MCAL:
                    ReadMCAL(ADTtexstream, mapname, chunkData); // alpha layers
                    break;
                default:
                    SkipUnknownChunk(ADTtexstream, chunkID, chunkSize);
                    break;
            }
        }
        ADTTexData.textureBlockData.textureChunksData.Add(chunkData);
    }

    public void ReadMTXF (MemoryStream ADTtexstream, int MTXFsize)
    {
        Debug.Log("MTXF : " + MTXFsize);
        ADTtexstream.Seek(MTXFsize, SeekOrigin.Current);
    }

    public void ReadMTXP (MemoryStream ADTtexstream, int MTXPsize) // 16 bytes per MTEX texture
    {
        StreamTools s = new StreamTools();
        Flags f = new Flags();
        ADTTexData.textureBlockData.MTXP = true;
        for (int i = 0; i < MTXPsize / 16; i++)
        {
            ADTTexData.textureBlockData.textureFlags.Add(ADTTexData.textureBlockData.terrainTexturePaths[i],f.ReadTerrainTextureFlag(ADTtexstream));
            // default 0.0 -- the _h texture values are scaled to [0, value) to determine actual "height".
            // this determines if textures overlap or not (e.g. roots on top of roads).
            ADTTexData.textureBlockData.heightScales.Add(ADTTexData.textureBlockData.terrainTexturePaths[i],s.ReadFloat(ADTtexstream));
            // default 1.0 -- note that _h based chunks are still influenced by MCAL (blendTex below)
            ADTTexData.textureBlockData.heightOffsets.Add(ADTTexData.textureBlockData.terrainTexturePaths[i],s.ReadFloat(ADTtexstream));
            // no default, no non-zero values in 20490
            int padding = s.ReadLong(ADTtexstream);
        }
    }

    /////////////////////////////
    ///// MCNKtex Subchunks /////
    /////////////////////////////

    public void ReadMCLY(MemoryStream ADTtexstream, ADTTexData.TextureChunkData chunkData, int MCLYsize)
    {
        /*
        *  Texture layer definitions for this map chunk. 16 bytes per layer, up to 4 layers (thus, layer count = size / 16).
        *  Every texture layer other than the first will have an alpha map to specify blending amounts. The first layer is rendered with full opacity. To know which alphamap is used, there is an offset into the MCAL chunk. That one is relative to MCAL.
        *  You can animate these by setting the flags. Only simple linear animations are possible. You can specify the direction in 45° steps and the speed.
        *  The textureId is just the array index of the filename array in the MTEX chunk.
        *  For getting the right feeling when walking, you should set the effectId which links to GroundEffectTextureRec::m_ID. It defines the little detail doodads as well as the footstep sounds and if footprints are visible. You can set the id to -1 (int16!) to have no detail doodads and footsteps at all. Also, you need to define the currently on-top layer in the MCNK structure for the correct detail doodads to show up!
        *  Introduced in Wrath of the Lich King, terrain can now reflect a skybox. This is used for icecubes made out of ADTs to reflect something. You need to have the MTXF chunk in, if you want that. Look at an skybox Blizzard made to see how you should do it.
        */
        if (MCLYsize == 0)
            return;

        StreamTools s = new StreamTools();
        long MCLYStartPosition = ADTtexstream.Position;
        int numberOfLayers = MCLYsize / 16;
        chunkData.NumberOfTextureLayers = numberOfLayers;
        chunkData.textureIds = new int[numberOfLayers];
        chunkData.LayerOffsetsInMCAL = new int[numberOfLayers];
        for (int l = 0; l < numberOfLayers; l++)
        {
            chunkData.textureIds[l] = s.ReadLong(ADTtexstream); // texture ID
            // <flags>
            byte[] arrayOfBytes = new byte[4];
            ADTtexstream.Read(arrayOfBytes, 0, 4);
            BitArray flags = new BitArray(arrayOfBytes);
            int animation_rotation = (flags[0] ? 1 : 0) + (flags[1] ? 1 : 0) + (flags[2] ? 1 : 0); // each tick is 45°
            int animation_speed = (flags[3] ? 1 : 0) + (flags[4] ? 1 : 0) + (flags[5] ? 1 : 0); // 0 to 3
            bool animation_enabled = flags[6];
            bool overbright = flags[7];                // This will make the texture way brighter. Used for lava to make it "glow".
            bool use_alpha_map = flags[8];             // set for every layer after the first
            chunkData.alpha_map_compressed[l] = flags[9];      // see MCAL chunk description - MCLY_AlphaType_Flag
            bool use_cube_map_reflection = flags[10];   // This makes the layer behave like its a reflection of the skybox. See below
            bool unknown_0x800 = flags[11];             // WoD?+ if either of 0x800 or 0x1000 is set, texture effects' texture_scale is applied
            bool unknown_0x1000 = flags[12];            // WoD?+ see 0x800
            // flags 13-32 unused
            // </flags>
            int layerOffset = s.ReadLong(ADTtexstream);
            chunkData.LayerOffsetsInMCAL[l] = layerOffset;
            int effectId = s.ReadLong(ADTtexstream); //foreign_keyⁱ <uint32_t, &GroundEffectTextureRec::m_ID>; // 0xFFFFFFFF for none, in alpha: uint16_t + padding
        }
    }

    public void ReadMCSH(MemoryStream ADTtexstream, ADTTexData.TextureChunkData chunkData) //512 bytes
    {
        // The shadows are stored per bit, not byte as 0 or 1 (off or on) so we have 8 bytes (which equates to 64 values) X 64 bytes (64 values in this case) which ends up as a square 64x64 shadowmap with either white or black.
        // Note that the shadow values come LSB first.
        // 8bytes(64values) x 64 = 512 bytes
        chunkData.shadowMap = new bool[64 * 64];
        chunkData.shadowMapTexture = new byte[64 * 64];

        byte[] ByteArray = new byte[64 * 64];
        ADTtexstream.Read(ByteArray, 0, 8 * 64);
        if (SettingsTerrainImport.LoadShadowMaps)
        {
            BitArray bits = new BitArray(ByteArray);
            for (int b = 4095; b >= 0; b--) // LSB
            {
                chunkData.shadowMap[b] = bits[b];
                if (bits[b])
                    chunkData.shadowMapTexture[b] = 127;
                else
                    chunkData.shadowMapTexture[b] = 0;
            }
        }
    }

    public void ReadMCAL(MemoryStream ADTtexstream, string mapname, ADTTexData.TextureChunkData chunkData)
    {
        StreamTools s = new StreamTools();
        long McalStartPosition = ADTtexstream.Position;
        int numberofLayers = chunkData.NumberOfTextureLayers;
        if (numberofLayers > 1)
        {
            chunkData.alphaLayers = new List<byte[]>();
            for (int l = 1; l < numberofLayers; l++) {
                if (WDT.Flags[mapname].adt_has_height_texturing == true)
                {
                    if (chunkData.alpha_map_compressed[l] == false)
                    {
                        chunkData.alphaLayers.Add(AlphaMap_UncompressedFullRes(ADTtexstream));
                    }
                    else if (chunkData.alpha_map_compressed[l] == true)
                    {
                        chunkData.alphaLayers.Add(AlphaMap_Compressed(ADTtexstream));
                    }
                }
                else if (WDT.Flags[mapname].adt_has_height_texturing == false)
                {
                    if (WDT.Flags[mapname].adt_has_big_alpha == false)
                    {
                        if (chunkData.alpha_map_compressed[l] == false)
                        {
                            chunkData.alphaLayers.Add(AlphaMap_UncompressedHalfRes(ADTtexstream));
                        }
                        else if (chunkData.alpha_map_compressed[l] == true)
                        {
                            chunkData.alphaLayers.Add(AlphaMap_Compressed(ADTtexstream));
                        }
                    }
                    else if (WDT.Flags[mapname].adt_has_big_alpha == true)
                    {
                        if (chunkData.alpha_map_compressed[l] == false)
                        {
                            chunkData.alphaLayers.Add(AlphaMap_UncompressedFullRes(ADTtexstream));
                        }
                        else if (chunkData.alpha_map_compressed[l] == true)
                        {
                            chunkData.alphaLayers.Add(AlphaMap_Compressed(ADTtexstream));
                        }
                    }
                }
            }
        }
    }

    public byte[] AlphaMap_UncompressedFullRes(MemoryStream ADTtexstream) // uncompressed 4096 bytes
    {
        byte[] textureArray = new byte[4096];
        ADTtexstream.Read(textureArray, 0, 4096);
        return textureArray;
    }

    public byte[] AlphaMap_Compressed(MemoryStream ADTtexstream) // compressed
    {
        StreamTools s = new StreamTools();
        byte[] textureArray = new byte[4096];
        int alphaOffset = 0;
        while (alphaOffset < 4096)
        {
            //read a byte//
            byte onebyte = (byte)ADTtexstream.ReadByte();
            //translate byte to 8 bits//
            byte[] bytearr = new byte[1];
            bytearr[0] = onebyte;
            BitArray bitarr = new BitArray(bytearr);
            //is first bit true?//
            bool fc = bitarr.Get(7); //true=fill, false=copy
            //next 7 bits determine how many times we fill/copy, max 127
            int[] array  = new int[1];
            bool[] bitArray = new bool[7];
            for (int i = 0; i < 7; i++) {
                bitArray[i] = bitarr.Get(6 - i);
            }
            int times = s.BoolArrayToInt(bitArray);
            if (times == 0)
            {
                alphaOffset = 4096;
                break;
            }
            if (times != 0)
            {
                //fill mode// 
                if (fc == true) // repeat the byte following the one we just read *count* number of times into the alpha map
                {
                    byte secondbytefill = (byte)ADTtexstream.ReadByte();
                    for (int j = 0; j < times; j++){
                        textureArray[alphaOffset] = secondbytefill;
                        alphaOffset++;
                    }
                }
                //copy mode//
                if (fc == false)  //  read *count* number of following bytes into the alpha map
                {
                    for (int jc = 0; jc < times; jc++) {
                        byte secondbytecopy = (byte)ADTtexstream.ReadByte();
                        textureArray[alphaOffset] = secondbytecopy;
                        alphaOffset++;
                    }
                }
            }
        }
        alphaOffset = 0;
        return textureArray;
    }

    public byte[] AlphaMap_UncompressedHalfRes(MemoryStream ADTtexstream)
    {
        StreamTools s = new StreamTools();
        int currentArrayPos = 0;
        byte[] textureArray = new byte[4096];
        for (int ux = 0 ; ux < 2048; ux++){
            byte onebyte = (byte)ADTtexstream.ReadByte();
            byte nibble1 = (byte)(onebyte & 0x0F);
            byte nibble2 = (byte)((onebyte & 0xF0) >> 4);
            int first = s.NormalizeHalfResAlphaPixel(nibble2);
            int second = s.NormalizeHalfResAlphaPixel(nibble1);
            textureArray[ux + currentArrayPos + 0] = (byte)first;
            textureArray[ux + currentArrayPos + 1] = (byte)second;
            currentArrayPos = currentArrayPos + 1;
        }
        currentArrayPos = 0;
        return textureArray;
    }

    // Move the stream forward upon finding unknown chunks //
    public void SkipUnknownChunk(MemoryStream ADTstream, int chunkID, int chunkSize)
    {
        try
        {
            //Debug.Log("Unknown chunk : " + (Enum.GetName(typeof(ADT), chunkID)).ToString() + " | Skipped");
        }
        catch
        {
            Debug.Log("Missing chunk ID : " + chunkID);
        }
        ADTstream.Seek(chunkSize, SeekOrigin.Current);
    }
}
