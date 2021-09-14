using System.Collections.Generic;
using System.IO;
using System.Text;
using LibOrbisPkg.PKG;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PlayGo
{
  public class ChunkDat : Entry
  {
    public uint magic;
    public ushort version_major;
    public ushort version_minor;
    public ushort image_count;
    public ushort chunk_count;
    public ushort mchunk_count;
    public ushort scenario_count;
    public uint file_size;
    public ushort default_scenario_id;
    public ushort attrib;
    public uint sdk_ver;
    public ushort disc_count;
    public ushort layer_bmp;
    public byte[] reserved;
    public string content_id;

    public List<ChunkAttr> ChunkAtrrs;
    public List<ushort> ChunkMchunks;
    public List<string> ChunkLabels;
    public List<MChunkAttr> MchunkAttrs;
    public List<MChunkAttr> InnerMChunkAttrs;
    public List<ScenarioAttr> ScenarioAttrs;
    public List<ushort> ScenarioChunks;
    public List<string> ScenarioLabels;

    public override EntryId Id => EntryId.PLAYGO_CHUNK_DAT;

    public override uint Length => file_size;

    public override string Name => "playgo-chunk.dat";

    public static ChunkDat FromProject(string contentId)
    {
      var dat = new ChunkDat
      {
        magic = 0x6f676c70, // 'plgo'
        version_major = 0x0000,
        version_minor = 0x0000,
        image_count = 1,
        chunk_count = 1,
        mchunk_count = 1,
        scenario_count = 1,
        file_size = 416,
        default_scenario_id = 0,
        attrib = 1,
        sdk_ver = 0,
        disc_count = 0,
        layer_bmp = 0,
        reserved = new byte[32].Fill((byte)0xff),
        content_id = contentId,
        ChunkAtrrs = new List<ChunkAttr>()
        {
          new ChunkAttr
          {
            flag = 0x80,
            image_disc_layer_no = 0,
            req_locus = 3,
            mchunk_count = 1,
            language_mask = 0xFFFFFFFFFFFFFFFFUL,
            mchunks_offset = 0,
            label_offset = 0
          }
        },
        ChunkMchunks = new List<ushort> { 0 },
        ChunkLabels = new List<string> { "Chunk #0" },
        MchunkAttrs = new List<MChunkAttr>
        {
          new MChunkAttr
          {
            offset = 0,
            size = 0, // must update this to outer pfs image size + pfs offset
          }
        },
        InnerMChunkAttrs = new List<MChunkAttr>
        {
          new MChunkAttr
          {
            offset = 0,
            size = 0, // must update this to inner pfs image size
          }
        },
        ScenarioAttrs = new List<ScenarioAttr>
        {
          new ScenarioAttr
          {
            type = 1,
            initial_chunk_count = 1,
            chunk_count = 1,
            chunks_offset = 0,
            label_offset = 0,
          },
        },
        ScenarioChunks = new List<ushort> { 0 },
        ScenarioLabels = new List<string> { "Scenario #0" },
      };
      return dat;
    }

    public override void Write(Stream s)
    {
      var start = s.Position;
      s.WriteUInt32LE(magic);
      s.WriteUInt16LE(version_major);
      s.WriteUInt16LE(version_minor);
      s.WriteUInt16LE(image_count);
      s.WriteUInt16LE(chunk_count);
      s.WriteUInt16LE(mchunk_count);
      s.WriteUInt16LE(scenario_count);
      s.WriteUInt32LE(file_size);
      s.WriteUInt16LE(default_scenario_id);
      s.WriteUInt16LE(attrib);
      s.WriteUInt32LE(sdk_ver);
      s.WriteUInt16LE(disc_count);
      s.WriteUInt16LE(layer_bmp);
      s.Write(reserved, 0, 32);
      s.Write(Encoding.ASCII.GetBytes(content_id), 0, 36);
      s.Position = start + 0xC0;
      s.WriteUInt32LE(256);
      s.WriteUInt32LE(32);
      s.WriteUInt32LE(288);
      s.WriteUInt32LE(2);
      s.WriteUInt32LE(304);
      s.WriteUInt32LE(9);
      s.WriteUInt32LE(320);
      s.WriteUInt32LE(16);
      s.WriteUInt32LE(352);
      s.WriteUInt32LE(32);
      s.WriteUInt32LE(384);
      s.WriteUInt32LE(2);
      s.WriteUInt32LE(400);
      s.WriteUInt32LE(12);
      s.WriteUInt32LE(336);
      s.WriteUInt32LE(16);
      s.Position = start + 0x100;
      foreach (var x in ChunkAtrrs) x.WriteTo(s);
      s.Position = start + 0x120;
      foreach (var x in ChunkMchunks) s.WriteUInt16LE(x);
      s.Position = start + 0x130;
      foreach (var x in ChunkLabels) s.Write(Encoding.ASCII.GetBytes(x), 0, x.Length);
      s.Position = start + 0x140;
      foreach (var x in MchunkAttrs) x.WriteTo(s);
      s.Position = start + 0x150;
      foreach (var x in InnerMChunkAttrs) x.WriteTo(s);
      s.Position = start + 0x160;
      foreach (var x in ScenarioAttrs) x.WriteTo(s);
      s.Position = start + 0x180;
      foreach (var x in ScenarioChunks) s.WriteUInt16LE(x);
      s.Position = start + 0x190;
      foreach (var x in ScenarioLabels) s.Write(Encoding.ASCII.GetBytes(x), 0, x.Length);
    }
  }

  public struct ChunkAttr
  {
    public byte flag;
    public byte image_disc_layer_no;
    public byte req_locus;
    public ushort mchunk_count;
    public ulong language_mask;
    public uint mchunks_offset;
    public uint label_offset;

    public void WriteTo(System.IO.Stream s)
    {
      s.WriteByte(flag);
      s.WriteByte(image_disc_layer_no);
      s.WriteByte(req_locus);
      s.Position += 0xB;
      s.WriteUInt16LE(mchunk_count);
      s.WriteUInt64LE(language_mask);
      s.WriteUInt32LE(mchunks_offset);
      s.WriteUInt32LE(label_offset);
    }
  }

  public class MChunkAttr
  {
    public ulong offset;
    public ulong size;
    public void WriteTo(System.IO.Stream s)
    {
      s.WriteUInt64LE(offset);
      s.WriteUInt64LE(size);
    }
  }
  public class ScenarioAttr
  {
    public byte type;
    public ushort initial_chunk_count;
    public ushort chunk_count;
    public uint chunks_offset;
    public uint label_offset;
    public void WriteTo(System.IO.Stream s)
    {
      s.WriteByte(type);
      s.Position += 0x13;
      s.WriteUInt16LE(initial_chunk_count);
      s.WriteUInt16LE(chunk_count);
      s.WriteUInt32LE(chunks_offset);
      s.WriteUInt32LE(label_offset);
    }
  }
}

