using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibOrbisPkg.PKG;
using LibOrbisPkg.PFS;
using System.IO;
using System.IO.MemoryMappedFiles;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.GP4
{
  /// <summary>
  /// Contains functionality to create a GP4 from a PKG
  /// </summary>
  public static class Gp4Creator
  {
    public static EntryId[] GeneratedEntries = new[]
    {
      EntryId.DIGESTS,
      EntryId.ENTRY_KEYS,
      EntryId.IMAGE_KEY,
      EntryId.GENERAL_DIGESTS,
      EntryId.METAS,
      EntryId.ENTRY_NAMES,
      EntryId.LICENSE_DAT,
      EntryId.LICENSE_INFO,
      EntryId.PSRESERVED_DAT,
      EntryId.PLAYGO_CHUNK_DAT,
      EntryId.PLAYGO_CHUNK_SHA,
      EntryId.PLAYGO_MANIFEST_XML,
    };

    public static void CreateProjectFromPKG(string outputDir, MemoryMappedFile pkgFile, string passcode = null)
    {
      Directory.CreateDirectory(outputDir);
      Pkg pkg;
      using (var f = pkgFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read))
        pkg = new PkgReader(f).ReadPkg();

      passcode = passcode ?? "00000000000000000000000000000000";

      // Initialize project parameters
      var project = Gp4Project.Create(ContentTypeToVolumeType(pkg.Header.content_type));
      project.volume.Package.Passcode = passcode;
      project.volume.Package.ContentId = pkg.Header.content_id;
      project.volume.Package.AppType = project.volume.Type == VolumeType.pkg_ps4_app ? "full" : null;
      project.volume.Package.StorageType = project.volume.Type == VolumeType.pkg_ps4_app ? "digital50" : null;

      if(pkg.Header.content_type == ContentType.AC || pkg.Header.content_type == ContentType.AL)
      {
        pkg.LicenseDat.DecryptSecretWithDebugKey();
        var entitlementKey = new byte[16];
        Buffer.BlockCopy(pkg.LicenseDat.Secret, 0x70, entitlementKey, 0, 16);
        pkg.LicenseDat.EncryptSecretWithDebugKey();
        project.volume.Package.EntitlementKey = entitlementKey.ToHexCompact();
      }

      // Extract entry filesystem
      var sys_dir = Path.Combine(outputDir, "sce_sys");
      var sys_projdir = project.AddDir(null, "sce_sys");
      Directory.CreateDirectory(sys_dir);
      foreach (var meta in pkg.Metas.Metas)
      {
        // Skip entries that are auto-generated or that we don't know the filenames for
        if (GeneratedEntries.Contains(meta.id)) continue;
        if (!EntryNames.IdToName.ContainsKey(meta.id)) continue;

        var entryName = EntryNames.IdToName[meta.id];
        var filename = Path.Combine(sys_dir, entryName);

        // Create directories for entries within directories
        if (entryName.Contains('/'))
        {
          var entryDir = entryName.Substring(0, entryName.LastIndexOf('/'));
          Directory.CreateDirectory(Path.Combine(sys_dir, entryDir));
          Dir d = sys_projdir;
          foreach (var breadcrumb in entryDir.Split('/'))
          {
            d = project.AddDir(d, breadcrumb);
          }
        }

        // Add the entry to the project
        project.files.Items.Add(new Gp4File()
        {
          OrigPath = "sce_sys/" + entryName,
          TargetPath = "sce_sys/" + entryName
        });

        // Save to the filesystem
        using (var s = pkgFile.CreateViewStream(meta.DataOffset, meta.DataSize, MemoryMappedFileAccess.Read))
        using (var entryFile = File.Create(filename))
        {
          s.CopyTo(entryFile);
        }
      }

      // Fixup the param.sfo
      using (var f = File.Open(Path.Combine(outputDir, "sce_sys/param.sfo"), FileMode.Open))
      {
        var sfo = SFO.ParamSfo.FromStream(f);
        var pubtoolinfo = (sfo["PUBTOOLINFO"] as SFO.Utf8Value).Value;
        var c_date = "";
        var c_time = "";
        foreach (var info in pubtoolinfo.Split(','))
        {
          var info2 = info.Split('=');
          switch (info2[0])
          {
            case "c_date":
              c_date = 
                info2[1].Substring(0,4) + "-" +
                info2[1].Substring(4,2) + "-" +
                info2[1].Substring(6,2);
              break;
            case "c_time":
              c_time = " " + 
                info2[1].Substring(0,2) + ":" +
                info2[1].Substring(2,2) + ":" +
                info2[1].Substring(4,2);
              break;
          }
        }
        project.volume.Package.CreationDate = c_date + c_time;
        sfo["PUBTOOLVER"] = null;
        sfo["PUBTOOLINFO"] = null;
        sfo.Write(f);
      }

      // Extract files from the PFS filesystem
      byte[] ekpfs;
      if (pkg.CheckPasscode(passcode))
      {
        ekpfs = Crypto.ComputeKeys(pkg.Header.content_id, passcode, 1);
      }
      else
      {
        ekpfs = pkg.GetEkpfs();
      }
      using (var va = pkgFile.CreateViewAccessor((long)pkg.Header.pfs_image_offset, (long)pkg.Header.pfs_image_size, MemoryMappedFileAccess.Read))
      {
        var outerPfs = new PfsReader(va, pkg.Header.pfs_flags, ekpfs);
        var inner = new PfsReader(new PFSCReader(outerPfs.GetFile("pfs_image.dat").GetView()));
        // Convert PFS image timestamp from UNIX time and save it in the project
        project.volume.TimeStamp = new DateTime(1970, 1, 1)
          .AddSeconds(inner.Header.InodeBlockSig.Time1_sec);
        var uroot = inner.GetURoot();
        Dir dir = null;
        var projectDirs = new Queue<Dir>();
        var pfsDirs = new Queue<PfsReader.Dir>();
        pfsDirs.Enqueue(uroot);
        projectDirs.Enqueue(dir);
        while (pfsDirs.Count > 0)
        {
          dir = projectDirs.Dequeue();
          if(dir != null)
          {
            Directory.CreateDirectory(Path.Combine(outputDir, dir.Path));
          }
          foreach (var f in pfsDirs.Dequeue().children)
          {
            if (f is PfsReader.Dir d)
            {
              pfsDirs.Enqueue(d);
              projectDirs.Enqueue(project.AddDir(dir, d.name));
            }
            else if (f is PfsReader.File file)
            {
              // Remove "/uroot/"
              var path = file.FullName.Substring(7);
              project.files.Items.Add(new Gp4File()
              {
                OrigPath = path,
                TargetPath = path
              });
              file.Save(Path.Combine(outputDir, path));
            }
          }
        }
      }

      // Last step: save the project file
      using (var f = File.Create(Path.Combine(outputDir, "Project.gp4")))
      {
        Gp4Project.WriteTo(project, f);
      }
    }

    /// <summary>
    /// Creates a GP4 project in the given output directory from the given pkg
    /// </summary>
    /// <param name="outputDir">Directory in which to save the project and files</param>
    /// <param name="pkgFilename">Path to the PKG file</param>
    /// <param name="passcode">The PKG's passcode</param>
    public static void CreateProjectFromPKG(string outputDir, string pkgFilename, string passcode = null)
    {
      using (var pkgFile = MemoryMappedFile.CreateFromFile(pkgFilename, FileMode.Open))
      {
        CreateProjectFromPKG(outputDir, pkgFile, passcode);
      }
    }

    private static VolumeType ContentTypeToVolumeType(ContentType t)
    {
      switch (t)
      {
        case ContentType.GD:
          return VolumeType.pkg_ps4_app;
        case ContentType.DP:
          return VolumeType.pkg_ps4_patch;
        case ContentType.AC:
          return VolumeType.pkg_ps4_ac_data;
        case ContentType.AL:
          return VolumeType.pkg_ps4_ac_nodata;
        default:
          return 0;
      }
    }
  }
}
