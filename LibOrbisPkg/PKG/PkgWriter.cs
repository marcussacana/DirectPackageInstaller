using LibOrbisPkg.Util;
using System.Linq;
using System.Text;
using System.IO;

namespace LibOrbisPkg.PKG
{
  public class PkgWriter : Util.WriterBase
  {
    public PkgWriter(Stream s) : base(true, s) { }

    public void WriteBody(Pkg pkg, string contentId, string passcode)
    {
      foreach (var entry in pkg.Entries)
      {
        s.Position = entry.meta.DataOffset;
        if (entry.meta.Encrypted)
        {
          entry.WriteEncrypted(s, contentId, passcode);
        }
        else
        {
          entry.Write(s);
        }
      }
    }

    public void WriteHeader(in Header hdr)
    {
      s.Position = 0x00;
      Write(Encoding.ASCII.GetBytes(hdr.CNTMagic));
      s.Position = 0x04;
      Write((uint)hdr.flags);
      s.Position = 0x08;
      Write(hdr.unk_0x08);
      s.Position = 0x0C;
      Write(hdr.unk_0x0C); /* 0xF */
      s.Position = 0x10;
      Write(hdr.entry_count);
      s.Position = 0x14;
      Write(hdr.sc_entry_count);
      s.Position = 0x16;
      Write(hdr.entry_count_2); /* same as entry_count */
      s.Position = 0x18;
      Write(hdr.entry_table_offset);
      s.Position = 0x1C;
      Write(hdr.main_ent_data_size);
      s.Position = 0x20;
      Write(hdr.body_offset);
      s.Position = 0x28;
      Write(hdr.body_size);
      s.Position = 0x40;
      Write(Encoding.ASCII.GetBytes(hdr.content_id)); // Length = PKG_CONTENT_ID_SIZE
      s.Position = 0x70;
      Write((uint)hdr.drm_type);
      s.Position = 0x74;
      Write((uint)hdr.content_type);
      s.Position = 0x78;
      Write((uint)hdr.content_flags);
      s.Position = 0x7C;
      Write(hdr.promote_size);
      s.Position = 0x80;
      Write(hdr.version_date);
      s.Position = 0x84;
      Write(hdr.version_hash);
      s.Position = 0x88;
      Write(hdr.unk_0x88); /* for delta patches only? */
      s.Position = 0x8C;
      Write(hdr.unk_0x8C); /* for delta patches only? */
      s.Position = 0x90;
      Write(hdr.unk_0x90); /* for delta patches only? */
      s.Position = 0x94;
      Write(hdr.unk_0x94); /* for delta patches only? */
      s.Position = 0x98;
      Write((uint)hdr.iro_tag);
      s.Position = 0x9C;
      Write(hdr.ekc_version); /* drm type version */
      s.Position = 0x100;
      Write(hdr.sc_entries1_hash);
      s.Position = 0x120;
      Write(hdr.sc_entries2_hash);
      s.Position = 0x140;
      Write(hdr.digest_table_hash);
      s.Position = 0x160;
      Write(hdr.body_digest);

      // TODO: i think these fields are actually members of element of container array
      s.Position = 0x400;
      Write(hdr.unk_0x400);
      s.Position = 0x404;
      Write(hdr.pfs_image_count);
      s.Position = 0x408;
      Write(hdr.pfs_flags);
      s.Position = 0x410;
      Write(hdr.pfs_image_offset);
      s.Position = 0x418;
      Write(hdr.pfs_image_size);
      s.Position = 0x420;
      Write(hdr.mount_image_offset);
      s.Position = 0x428;
      Write(hdr.mount_image_size);
      s.Position = 0x430;
      Write(hdr.package_size);
      s.Position = 0x438;
      Write(hdr.pfs_signed_size);
      s.Position = 0x43C;
      Write(hdr.pfs_cache_size);
      s.Position = 0x440;
      Write(hdr.pfs_image_digest);
      s.Position = 0x460;
      Write(hdr.pfs_signed_digest);
      s.Position = 0x480;
      Write(hdr.pfs_split_size_nth_0);
      s.Position = 0x488;
      Write(hdr.pfs_split_size_nth_1);
    }
  }
}
