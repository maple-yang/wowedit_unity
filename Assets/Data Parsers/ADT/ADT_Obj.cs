﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static partial class ADT
{

    // List of filenames for M2 models that appear in this map tile. //
    public static void ReadMMDX(MemoryStream ADTobjstream, int MMDXsize)
    {
        StreamTools s = new StreamTools();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MMDXsize)
        {
            int position = (int)(ADTobjstream.Position - currentPos);
            string path = s.ReadNullTerminatedString(ADTobjstream);
            if (path != "")
            {
                modelBlockData.M2Paths.Add(position, path);
            }
        }
    }

    // List of offsets of model filenames in the MMDX chunk. //
    public static void ReadMMID(MemoryStream ADTobjstream, int MMIDsize)
    {
        StreamTools s = new StreamTools();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MMIDsize)
        {
            modelBlockData.M2Offsets.Add(s.ReadLong(ADTobjstream));
        }
    }

    // List of filenames for WMOs (world map objects) that appear in this map tile. //
    public static void ReadMWMO(MemoryStream ADTobjstream, int MWMOsize)
    {
        StreamTools s = new StreamTools();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MWMOsize)
        {
            int position = (int)(ADTobjstream.Position - currentPos);
            string path = s.ReadNullTerminatedString(ADTobjstream);
            if (path != "")
            {
                modelBlockData.WMOPaths.Add(position, path);
            }
        }
    }

    // List of offsets of WMO filenames in the MWMO chunk. //
    public static void ReadMWID(MemoryStream ADTobjstream, int MWIDsize)
    {
        StreamTools s = new StreamTools();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MWIDsize)
        {
            modelBlockData.WMOOffsets.Add(s.ReadLong(ADTobjstream));
        }
    }

    // Placement information for doodads (M2 models). //
    // Additional to this, the models to render are referenced in each MCRF chunk. //
    public static void ReadMDDF(MemoryStream ADTobjstream, int MDDFsize)
    {
        Flags f = new Flags();
        StreamTools s = new StreamTools();
        modelBlockData.M2Info = new List<M2PlacementInfo>();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MDDFsize)
        {
            M2PlacementInfo data = new M2PlacementInfo();

            // References an entry in the MMID chunk, specifying the model to use.
            data.nameID = s.ReadLong(ADTobjstream);

            // This ID should be unique for all ADTs currently loaded.
            // Best, they are unique for the whole map. Blizzard has these unique for the whole game.
            data.uniqueID = s.ReadLong(ADTobjstream);

            // This is relative to a corner of the map. Subtract 17066 from the non vertical values and you should start to see 
            // something that makes sense. You'll then likely have to negate one of the non vertical values in whatever coordinate 
            // system you're using to finally move it into place.
            float Y = ((s.ReadFloat(ADTobjstream) - 17066) * -1) / Settings.worldScale; //-- pos X
            float Z = (s.ReadFloat(ADTobjstream)) / Settings.worldScale; //-- Height
            float X = ((s.ReadFloat(ADTobjstream) - 17066) * -1) / Settings.worldScale; //-- pos Z
            data.position = new Vector3(X, Z, Y);

            // degrees. This is not the same coordinate system orientation like the ADT itself! (see history.)
            float rotX = s.ReadFloat(ADTobjstream); //-- rot X
            float rotZ = 180 - s.ReadFloat(ADTobjstream); //-- rot Y
            float rotY = s.ReadFloat(ADTobjstream); //-- rot Z
            data.rotation = Quaternion.Euler(new Vector3(rotX, rotZ, rotY));

            // 1024 is the default size equaling 1.0f.
            data.scale = s.ReadShort(ADTobjstream) / 1024.0f;

            // values from struct MDDFFlags.
            data.flags = f.ReadMDDFFlags(ADTobjstream);

            modelBlockData.M2Info.Add(data);
        }
    }

    // Placement information for WMOs. //
    // Additional to this, the WMOs to render are referenced in each MCRF chunk. (?) //
    public static void ReadMODF(MemoryStream ADTobjstream, int MODFsize)
    {
        Flags f = new Flags();
        StreamTools s = new StreamTools();
        modelBlockData.WMOInfo = new List<WMOPlacementInfo>();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MODFsize)
        {
            WMOPlacementInfo data = new WMOPlacementInfo();

            // references an entry in the MWID chunk, specifying the model to use.
            data.nameID = s.ReadLong(ADTobjstream);

            // this ID should be unique for all ADTs currently loaded. Best, they are unique for the whole map.
            data.uniqueID = s.ReadLong(ADTobjstream);

            // same as in MDDF.
            float Y = ((s.ReadFloat(ADTobjstream) - 17066) * -1) / Settings.worldScale; //-- pos X
            float Z = (s.ReadFloat(ADTobjstream)) / Settings.worldScale; //-- Height
            float X = ((s.ReadFloat(ADTobjstream) - 17066) * -1) / Settings.worldScale; //-- pos Z
            data.position = new Vector3(X, Z, Y);

            // same as in MDDF.
            float rotX = s.ReadFloat(ADTobjstream); //-- rot X
            float rotZ = 180 - s.ReadFloat(ADTobjstream); //-- rot Y
            float rotY = s.ReadFloat(ADTobjstream); //-- rot Z
            data.rotation = Quaternion.Euler(new Vector3(rotX, rotZ, rotY));

            // position plus the transformed wmo bounding box. used for defining if they are rendered as well as collision.
            data.extents = s.ReadBoundingBox(ADTobjstream);

            // values from enum MODFFlags.
            data.flags = f.ReadMODFFlags(ADTobjstream);

            // which WMO doodad set is used.
            data.doodadSet = s.ReadShort(ADTobjstream);

            // which WMO name set is used. Used for renaming goldshire inn to northshire inn while using the same model.
            data.nameSet = s.ReadShort(ADTobjstream);

            // Legion(?)+: has data finally, looks like scaling (same as MDDF). Padding in 0.5.3 alpha. 
            int unk = s.ReadShort(ADTobjstream);

            modelBlockData.WMOInfo.Add(data);
        }
    }

    // Chunk Data //
    public static void ReadMCNKObj (MemoryStream ADTobjstream, string mapname, int MCNKchunkNumber, int MCNKsize)
    {
        if (ADTobjstream.Length == ADTobjstream.Position)
            return;
        StreamTools s = new StreamTools();
        long MCNKchnkPos = ADTobjstream.Position;
        long streamPosition = ADTobjstream.Position;
        while (streamPosition < MCNKchnkPos + MCNKsize)
        {
            ADTobjstream.Position = streamPosition;
            int chunkID = s.ReadLong(ADTobjstream);
            int chunkSize = s.ReadLong(ADTobjstream);
            streamPosition = ADTobjstream.Position + chunkSize;
            switch (chunkID)
            {
                case (int)ADTchunkID.MCRD:
                    ReadMCRD(ADTobjstream, MCNKchunkNumber, chunkSize); // MCNK.nDoodadRefs into the file's MDDF
                    break;
                case (int)ADTchunkID.MCRW:
                    ReadMCRW(ADTobjstream, MCNKchunkNumber, chunkSize); // MCNK.nMapObjRefs into the file's MODF
                    break;
                default:
                    SkipUnknownChunk(ADTobjstream, chunkID, chunkSize);
                    break;
            }
        }
    }

    /////////////////////
    ///// Subchunks /////
    /////////////////////

    // MCNK.nDoodadRefs into the file's MDDF //
    public static void ReadMCRD(MemoryStream ADTobjstream, int MCNKchunkNumber, int MCRDsize)
    {
        StreamTools s = new StreamTools();
        List<int> MDDFentries = new List<int>();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MCRDsize)
        {
            MDDFentries.Add(s.ReadLong(ADTobjstream));
        }
    }

    // MCNK.nMapObjRefs into the file's MODF //
    public static void ReadMCRW(MemoryStream ADTobjstream, int MCNKchunkNumber, int MCRWsize)
    {
        StreamTools s = new StreamTools();
        List<int> MODFentries = new List<int>();
        long currentPos = ADTobjstream.Position;
        while (ADTobjstream.Position < currentPos + MCRWsize)
        {
            MODFentries.Add(s.ReadLong(ADTobjstream));
        }
    }
}
