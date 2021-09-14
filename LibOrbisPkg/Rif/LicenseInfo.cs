using System.IO;
using System.Text;
using LibOrbisPkg.PKG;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.Rif
{
  class LicenseInfo
  {
    public LicenseInfo(string contentId, ContentType type, byte[] entitlement)
    {
      ContentId = contentId;
      ContentType = type;
      EntitlementKey = entitlement;
      Unknown_40 = ContentType == ContentType.AL ? 1 : 0;
      Unknown_48 = 0;
      Unknown_4C = 1;
    }
    public string ContentId;
    public byte[] EntitlementKey;
    public int Unknown_40;
    public ContentType ContentType = ContentType.AC;
    public int Unknown_48;
    public int Unknown_4C;
  }

  class LicenseInfoWriter : WriterBase
  {
    public LicenseInfoWriter(Stream stream) : base(true, stream) { }

    public void Write(LicenseInfo dat)
    {
      Write(Encoding.ASCII.GetBytes(dat.ContentId));
      Write(new byte[12]);
      Write(dat.EntitlementKey ?? new byte[16]);
      Write(dat.Unknown_40);
      Write((int)dat.ContentType);
      Write(dat.Unknown_48);
      Write(dat.Unknown_4C);
    }
  }
}
