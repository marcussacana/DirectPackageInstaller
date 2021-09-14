using System.Linq;
using System.Xml.Serialization;

namespace LibOrbisPkg.PlayGo
{
  [XmlRoot(ElementName = "psproject")]
  class Manifest
  {
    [XmlAttribute("fmt")]
    public string Format;
    [XmlAttribute("version")]
    public string version;
    [XmlElement(ElementName = "chunk_info")]
    public GP4.ChunkInfo chunk_info;


    public static void WriteTo(Manifest proj, System.IO.Stream s)
    {
      XmlSerializer mySerializer = new XmlSerializer(typeof(Manifest));
      mySerializer.Serialize(s, proj);
    }

    public static Manifest ReadFrom(System.IO.Stream s)
    {
      XmlSerializer mySerializer = new XmlSerializer(typeof(Manifest));
      var proj = (Manifest)mySerializer.Deserialize(s);
      return proj;
    }

    public static Manifest FromProject(GP4.Gp4Project project)
    {
      var man = new Manifest
      {
        Format = "playgo-manifest",
        version = "0990",
        chunk_info = project.volume.chunk_info
      };
      man.chunk_info.chunks = null;
      return man;
    }

    public static byte[] Default =
      ("ï»¿<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\r\n" +
      "<psproject fmt=\"playgo-manifest\" version=\"0990\">\r\n" +
      "  <volume>\r\n" +
      "    <chunk_info chunk_count=\"1\" scenario_count=\"1\">\r\n" +
      "      <scenarios default_id=\"0\">\r\n" +
      "        <scenario id=\"0\" type=\"sp\" initial_chunk_count=\"1\" label=\"Scenario #0\">0</scenario>\r\n" +
      "      </scenarios>\r\n" +
      "    </chunk_info>\r\n" +
      "  </volume>\r\n" +
      "</psproject>\r\n").Select(x => (byte)x).ToArray();
  }
}
