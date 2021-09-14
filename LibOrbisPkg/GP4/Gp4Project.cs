using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace LibOrbisPkg.GP4
{
  /// <summary>
  /// Represents a project file in the GP4 format. This is an XML format used by
  /// the "official" package tool to describe PKG images. Many samples can be found on GitHub.
  /// </summary>
  [XmlRoot(ElementName = "psproject")]
  public class Gp4Project
  {
    [XmlAttribute("fmt")]
    public string Format;
    [XmlAttribute("version")]
    public int version;
    [XmlElement(ElementName = "volume")]
    public Volume volume;
    [XmlElement(ElementName = "files")]
    public Files files;
    [XmlArrayItem(Type = typeof(Dir), ElementName = "dir")]
    [XmlArray(ElementName = "rootdir")]
    public List<Dir> RootDir = new List<Dir>();

    /// <summary>
    /// Writes the GP4 project xml to the given stream at the stream's current position.
    /// </summary>
    /// <param name="proj">The project to write</param>
    /// <param name="s">The stream to write the project to</param>
    public static void WriteTo(Gp4Project proj, System.IO.Stream s)
    {
      XmlSerializer mySerializer = new XmlSerializer(typeof(Gp4Project));
      mySerializer.Serialize(s, proj);
    }

    /// <summary>
    /// Reads a GP4 project from the given stream at the stream's current position.
    /// Probably throws an exception if you don't give a valid GP4?
    /// </summary>
    /// <param name="s">The stream to read from</param>
    /// <returns>A deserialized GP4 project</returns>
    public static Gp4Project ReadFrom(System.IO.Stream s)
    {
      XmlSerializer mySerializer = new XmlSerializer(typeof(Gp4Project));
      var proj = (Gp4Project)mySerializer.Deserialize(s);
      if(proj.volume.chunk_info?.ScenariosInfo?.Count() == 0)
      {
        proj.volume.chunk_info.ScenariosInfo = null;
      }

      // Fixup dir tree. For convenience we make our directory tree "doubly linked" in memory
      void setParent(List<Dir> dirs, Dir parent)
      {
        foreach(var dir in dirs)
        {
          dir.Parent = parent;
          setParent(dir.Children, dir);
        }
      }
      setParent(proj.RootDir, null);

      // We want each file entry to have an explicit path to simplify later code.
      foreach(var file in proj.files.Items)
      {
        if(file.OrigPath == null)
        {
          file.OrigPath = file.TargetPath.Replace('/', System.IO.Path.DirectorySeparatorChar);
        }
      }

      // An entitlement key is required for AC PKGs. So here we ensure that one exists.
      if(proj.volume.Package.EntitlementKey == null
        && (proj.volume.Type == VolumeType.pkg_ps4_ac_data
        || proj.volume.Type == VolumeType.pkg_ps4_ac_nodata))
      {
        proj.volume.Package.EntitlementKey = "00000000000000000000000000000000";
      }
      return proj;
    }

    /// <summary>
    /// Creates a new, empty GP4 project with the given VolumeType.
    /// </summary>
    /// <param name="type">The type of project to make</param>
    /// <returns>A new blank project with defaults for the given VolumeType</returns>
    public static Gp4Project Create(VolumeType type)
    {
      var proj = new Gp4Project
      {
        files = new Files(),
        Format = "gp4",
        RootDir = new List<Dir>(),
        version = 1000,
        volume = new Volume
        {
          volume_ts = DateTime.UtcNow.ToString("s").Replace('T', ' '),
          Package = new PackageInfo
          {
            ContentId = "XXXXXX-CUSA00000_00-ZZZZZZZZZZZZZZZZ",
            Passcode = "00000000000000000000000000000000"
          }
        }
      };
      proj.SetType(type);
      return proj;
    }

    #region Modification Functions

    /// <summary>
    /// Sets the target name of the given file to the given name.
    /// </summary>
    /// <param name="f">The file to rename</param>
    /// <param name="newName">The new filename</param>
    public void RenameFile(Gp4File f, string newName)
    {
      f.TargetPath = f.DirName + newName;
    }

    /// <summary>
    /// Renames the given directory and fixes up path names for all subfolders and files.
    /// </summary>
    /// <param name="d">Directory to rename</param>
    /// <param name="newName">New name for the directory.</param>
    public void RenameDir(Dir d, string newName)
    {
      var origPath = d.Path;
      d.TargetName = newName;
      var newPath = d.Path;
      foreach (var file in files.Items)
      {
        if (file.TargetPath.StartsWith(origPath))
        {
          file.TargetPath = newPath + file.FileName;
        }
      }
    }

    /// <summary>
    /// Deletes the given file from this project.
    /// </summary>
    public void DeleteFile(Gp4File f)
    {
      files.Items.Remove(f);
    }

    /// <summary>
    /// Deletes the directory and all files and subdirectories.
    /// </summary>
    public void DeleteDir(Dir d)
    {
      var path = d.Path;
      var deleteQueue = new List<Gp4File>();
      // This covers all children files, too.
      foreach (var f in files.Items)
      {
        if (f.TargetPath.StartsWith(path))
          deleteQueue.Add(f);
      }
      foreach (var f in deleteQueue)
      {
        files.Items.Remove(f);
      }
      RootDir.Remove(d);
      DeleteDirs(d);
    }

    /// <summary>
    /// Creates a new directory under the given parent directory with the given name.
    /// </summary>
    /// <param name="parent">Parent directory for the new directory</param>
    /// <param name="name">The new directory's name</param>
    /// <returns>The new directory</returns>
    public Dir AddDir(Dir parent, string name)
    {
      // Idempotence
      var existingDir = (parent?.Children ?? RootDir)
        .Where(d => d.TargetName == name)
        .FirstOrDefault();
      if (existingDir != null)
        return existingDir;

      var newDir = new Dir
      {
        TargetName = name,
        Parent = parent,
        Children = new List<Dir>(),
      };
      (parent?.Children ?? RootDir).Add(newDir);
      return newDir;
    }

    /// <summary>
    /// Unlinks all directories in the given directory's subtree.
    /// </summary>
    private static void DeleteDirs(Dir dir)
    {
      dir.Parent?.Children.Remove(dir);
      dir.Parent = null;
      // Work on a copy of the children so we can modify the original list
      foreach (var d2 in dir.Children.ToList())
        DeleteDirs(d2);
      dir.Children.Clear();
      dir.Children = null;
    }

    /// <summary>
    /// Sets the type of this project to the given type and modifies the project's data
    /// accordingly to ensure the project is valid.
    /// 
    /// For example, changing the type to a pkg_ps4_app will create PlayGo data;
    /// changing the type to an AC pkg will create an Entitlement key.
    /// </summary>
    /// <param name="type">The new volume type for the project</param>
    public void SetType(VolumeType type)
    {
      if (volume.volume_type != null && type == volume.Type) return;
      switch (type)
      {
        case VolumeType.pkg_ps4_app:
          volume.Package.EntitlementKey = null;
          volume.Package.StorageType = "digital50";
          volume.Package.AppType = "full";
          volume.chunk_info = new ChunkInfo
          {
            chunks = new List<Chunk>
            {
              new Chunk
              {
                id = 0,
                layer_no = 0,
                label = "Chunk #0",
              }
            },
            chunk_count = 1,
            scenarios = new Scenarios
            {
              default_id = 0,
              scenarios = new List<Scenario>
              {
                new Scenario
                {
                  id = 0,
                  type = "sp",
                  initial_chunk_count = 1,
                  label = "Scenario #0",
                  chunks = "0",
                }
              }
            },
            scenario_count = 1
          };
          break;
        case VolumeType.pkg_ps4_ac_data:
          volume.Package.EntitlementKey = "00000000000000000000000000000000";
          volume.Package.StorageType = null;
          volume.Package.AppType = null;
          volume.chunk_info = null;
          break;
        case VolumeType.pkg_ps4_ac_nodata:
          goto case VolumeType.pkg_ps4_ac_data;
        default:
          throw new Exception("Sorry, don't know how to make that project type!");
      }
      volume.Type = type;
    }
    #endregion
  }

  /// <summary>
  /// This is an element of the GP4 project that defines certain metadata for the PKG.
  /// </summary>
  public class Volume
  {
    [XmlElement(ElementName = "volume_type")]
    public string volume_type;
    [XmlIgnore]
    public VolumeType Type
    {
      get => VolumeTypeUtil.OfString(volume_type);
      set => volume_type = value.ToString();
    }
    [XmlElement(ElementName = "volume_id")]
    public string Id;
    [XmlElement(ElementName = "volume_ts")]
    public string volume_ts;
    [XmlIgnore]
    public DateTime TimeStamp
    {
      get => DateTime.Parse(volume_ts).ToUniversalTime();
      set => volume_ts = value.ToString("s").Replace('T', ' ');
    }
    [XmlElement(ElementName = "package")]
    public PackageInfo Package;
    [XmlElement(ElementName = "chunk_info")]
    public ChunkInfo chunk_info;
  }

  public enum VolumeType
  {
    bd25,
    bd50,
    bd50_50,
    bd50_25,
    bd25_50,
    exfat,
    pfs_plain,
    pfs_signed,
    pfs_nested,
    pkg_ps4_app,
    pkg_ps4_patch,
    pkg_ps4_remaster,
    pkg_ps4_ac_data,
    pkg_ps4_ac_nodata,
    pkg_ps4_sf_theme,
    pkg_ps4_theme,
  }

  public static class VolumeTypeUtil
  {
    public static VolumeType OfString(string s)
    {
      switch (s)
      {
        case "bd25": return VolumeType.bd25;
        case "bd50": return VolumeType.bd50;
        case "bd50_50": return VolumeType.bd50_50;
        case "bd50_25": return VolumeType.bd50_25;
        case "bd25_50": return VolumeType.bd25_50;
        case "exfat": return VolumeType.exfat;
        case "pfs_plain": return VolumeType.pfs_plain;
        case "pfs_signed": return VolumeType.pfs_signed;
        case "pfs_nested": return VolumeType.pfs_nested;
        case "pkg_ps4_app": return VolumeType.pkg_ps4_app;
        case "pkg_ps4_patch": return VolumeType.pkg_ps4_patch;
        case "pkg_ps4_remaster": return VolumeType.pkg_ps4_remaster;
        case "pkg_ps4_ac_data": return VolumeType.pkg_ps4_ac_data;
        case "pkg_ps4_ac_nodata": return VolumeType.pkg_ps4_ac_nodata;
        case "pkg_ps4_sf_theme": return VolumeType.pkg_ps4_sf_theme;
        case "pkg_ps4_theme": return VolumeType.pkg_ps4_theme;
        default: throw new Exception("Unknown Volume Type: " + s);
      }
    }
  }

  /// <summary>
  /// This is the &lt;package&gt; element within the &lt;volume&gt; element.
  /// </summary>
  public class PackageInfo
  {
    [XmlAttribute("content_id")]
    public string ContentId;
    [XmlAttribute("passcode")]
    public string Passcode;
    [XmlAttribute("entitlement_key")]
    public string EntitlementKey;
    [XmlAttribute("storage_type")]
    public string StorageType;
    [XmlAttribute("app_type")]
    public string AppType;
    [XmlAttribute("c_date")]
    public string CreationDate;
    [XmlIgnore]
    public DateTime CreationTimeStamp
    {
      get => DateTime.Parse(CreationDate).ToUniversalTime();
    }
  }

  /// <summary>
  /// A list of files; a child of the psproject element.
  /// </summary>
  public class Files
  {
    [XmlAttribute("img_no")]
    public int ImageNum;
    [XmlElement(ElementName = "file")]
    public List<Gp4File> Items = new List<Gp4File>();
  }

  /// <summary>
  /// Represents a file in the GP4 project.
  /// </summary>
  public class Gp4File
  {
    [XmlAttribute("targ_path")]
    public string TargetPath;
    [XmlAttribute("orig_path")]
    public string OrigPath;
    public string FileName => TargetPath.Substring(TargetPath.LastIndexOf('/') + 1);
    public string DirName => TargetPath.Substring(0, TargetPath.LastIndexOf('/') + 1);
  }

  /// <summary>
  /// Represents a directory in the GP4 project.
  /// </summary>
  public class Dir
  {
    [XmlAttribute("targ_name")]
    public string TargetName;
    [XmlElement(ElementName = "dir")]
    public List<Dir> Children = new List<Dir>();
    [XmlIgnore]
    public Dir Parent;
    public string Path
    {
      get
      {
        var prefix = "";
        var dir = this;
        while (dir != null)
        {
          prefix = dir.TargetName + "/" + prefix;
          dir = dir.Parent;
        }
        return prefix;
      }
    }
  }

  /// <summary>
  /// This element is part of the GP4 project only for game/app packages.
  /// It is for PlayGo.
  /// </summary>
  public class ChunkInfo
  {
    [XmlAttribute(AttributeName = "chunk_count")]
    public int chunk_count;

    [XmlAttribute(AttributeName = "scenario_count")]
    public int scenario_count;

    [XmlArrayItem(Type = typeof(Chunk), ElementName = "chunk")]
    [XmlArray(ElementName = "chunks")]
    public List<Chunk> chunks = new List<Chunk>();

    [XmlElement(ElementName = "scenarios")]
    public Scenarios scenarios;

    [XmlArrayItem(Type = typeof(ScenarioInfo), ElementName = "scenario_info")]
    [XmlArray(ElementName = "scenarios_info")]
    public List<ScenarioInfo> ScenariosInfo;
  }

  /// <summary>
  /// This element is a child of ChunkInfo
  /// </summary>
  public class Chunk
  {
    [XmlAttribute(AttributeName = "id")]
    public int id;
    [XmlAttribute(AttributeName = "layer_no")]
    public int layer_no;
    [XmlAttribute(AttributeName = "label")]
    public string label;
  }

  /// <summary>
  /// This element is a child of ChunkInfo
  /// </summary>
  public class Scenarios
  {
    [XmlAttribute(AttributeName = "default_id")]
    public int default_id;

    [XmlElement(Type = typeof(Scenario), ElementName = "scenario")]
    public List<Scenario> scenarios = new List<Scenario>();
  }

  /// <summary>
  /// This element is a child of ChunkInfo
  /// </summary>
  public class Scenario
  {
    [XmlAttribute(AttributeName = "id")]
    public int id;
    [XmlAttribute(AttributeName = "type")]
    public string type;
    [XmlAttribute(AttributeName = "initial_chunk_count")]
    public int initial_chunk_count;
    [XmlAttribute(AttributeName = "label")]
    public string label;
    [XmlText]
    public string chunks;
  }

  /// <summary>
  /// This element is a child of ChunkInfo
  /// </summary>
  public class ScenarioInfo
  {
    [XmlAttribute("id")]
    public int id;
    [XmlAttribute("type")]
    public string type;

    [XmlElement(ElementName = "lang")]
    public List<ScenarioLang> Langs;
  }

  /// <summary>
  /// This element is a child of ScenarioInfo
  /// </summary>
  public class ScenarioLang
  {
    [XmlAttribute("type")]
    public string type;
    [XmlAttribute("name")]
    public string name;
  }
}
