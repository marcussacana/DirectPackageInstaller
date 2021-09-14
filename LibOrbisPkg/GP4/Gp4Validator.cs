using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using LibOrbisPkg.SFO;

namespace LibOrbisPkg.GP4
{
  public class ValidateResult
  {
    public enum ResultType
    {
      Warning,
      Fatal,
    }
    public readonly ResultType Type;
    public readonly string Message;
    internal ValidateResult(ResultType t, string m)
    {
      Type = t;
      Message = m;
    }
    public static ValidateResult Fatal(string message)
    {
      return new ValidateResult(ResultType.Fatal, message);
    }
    public static ValidateResult Warning(string messsage)
    {
      return new ValidateResult(ResultType.Warning, messsage);
    }
  }
  public class Gp4Validator
  {
    private static ValidateResult checkPasscode(Gp4Project proj, string dir)
    {
      if (proj.volume.Package.Passcode.Length != 32)
      {
        return ValidateResult.Fatal("Passcode must be 32 characters long.");
      }
      return null;
    }
    private static ValidateResult checkAllFilesExist (Gp4Project proj, string dir)
    {
      var missingFiles = proj.files.Items
        .Where(f => Path.Combine(dir, f.OrigPath) is string filePath && !File.Exists(filePath))
        .Aggregate((string)null, (s, f) => s == null ? f.OrigPath : (s + ", " + f.OrigPath));
      if (missingFiles != null)
      {
        return ValidateResult.Fatal("Could not find source file(s): " + missingFiles);
      }
      return null;
    }
    private static ValidateResult checkDuplicateFilenames(Gp4Project proj, string dir)
    {
      var dupeFiles = proj.files.Items
        .GroupBy(f => f.TargetPath)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .Aggregate((string)null, (s, f) => s == null ? f : (s + ", " + f));
      if (dupeFiles != null)
      {
        return ValidateResult.Fatal("PKG has duplicate filename(s): " + dupeFiles);
      }
      return null;
    }
    private static ValidateResult checkContentIdFormat(Gp4Project proj, string dir)
    {
      var pkgContentId = proj.volume.Package.ContentId;
      Regex contentIdReg = new Regex("^[A-Z]{2}[0-9]{4}-[A-Z]{4}[0-9]{5}_00-[A-Z0-9]{16}$");
      if (contentIdReg.IsMatch(pkgContentId))
      {
        return null;
      }
      return ValidateResult.Warning(
        "PKG Content ID is the wrong format. " +
        "Format should be XXYYYY-XXXXYYYYY_00-ZZZZZZZZZZZZZZZZ, where X is a letter, Y is a number, and Z is either.");
    }
    private static ValidateResult checkContentIdLength(Gp4Project proj, string dir)
    {
      if (proj.volume.Package.ContentId.Length != 36)
      {
        return ValidateResult.Fatal("PKG Content ID must be 36 characters long.");
      }
      return null;
    }
    private static ValidateResult checkPkgVolumeType(Gp4Project proj, string dir)
    {
      var pkgType = proj.volume.Type;
      switch (pkgType)
      {
        case VolumeType.pkg_ps4_app:
          break;
        case VolumeType.pkg_ps4_ac_data:
          break;
        case VolumeType.pkg_ps4_ac_nodata:
          break;
        default:
          return ValidateResult.Fatal(
            "Unsupported PKG volume type: " + pkgType);
      }
      return null;
    }
    private static List<Func<Gp4Project, string, ValidateResult>> commonChecks = new List<Func<Gp4Project, string, ValidateResult>>()
    {
      checkPasscode,
      checkAllFilesExist,
      checkDuplicateFilenames,
      checkContentIdLength,
      checkContentIdFormat,
      checkPkgVolumeType,
    };
    private static ValidateResult checkContentIdMatchesSfo(Gp4Project proj, string dir, ParamSfo sfo)
    {
      var pkgContentId = proj.volume.Package.ContentId;
      var sfoContentId = sfo["CONTENT_ID"].ToString();
      if (pkgContentId != sfoContentId)
      {
        return ValidateResult.Warning(
          $"PKG Content ID {pkgContentId} does not match CONTENT_ID {sfoContentId} in param.sfo.");
      }
      return null;
    }
    private static ValidateResult checkContentIdMatchesTitleIdSfo(Gp4Project proj, string dir, ParamSfo sfo)
    {
      var sfoContentId = sfo["CONTENT_ID"].ToString();
      var sfoTitleId = sfo["TITLE_ID"].ToString();
      if (sfoContentId.Substring(7).StartsWith(sfoTitleId))
      {
        return null;
      }
      return ValidateResult.Warning(
        $"SFO TITLE_ID {sfoTitleId} does not match the CONTENT_ID {sfoContentId}.");
    }
    private static ValidateResult checkCategoryMatchesPkgType(Gp4Project proj, string dir, ParamSfo sfo)
    {
      var sfoCategory = sfo["CATEGORY"].ToString();
      var pkgType = proj.volume.Type;
      bool ok = true;
      switch (pkgType)
      {
        case VolumeType.pkg_ps4_app:
          if (!sfoCategory.StartsWith("g"))
            ok = false;
          break;
        case VolumeType.pkg_ps4_ac_data:
        case VolumeType.pkg_ps4_ac_nodata:
        case VolumeType.pkg_ps4_theme:
        case VolumeType.pkg_ps4_sf_theme:
          if (sfoCategory != "ac")
            ok = false;
          break;
      }
      if (!ok)
      {
        return ValidateResult.Warning(
           $"SFO CATEGORY {sfoCategory} is not valid for PKG volume type {pkgType}.");
      }
      return null;
    }
    private static List<Func<Gp4Project, string, ParamSfo, ValidateResult>> sfoChecks = new List<Func<Gp4Project, string, ParamSfo, ValidateResult>>()
    {
      checkContentIdMatchesSfo,
      checkContentIdMatchesTitleIdSfo,
      checkCategoryMatchesPkgType
    };

    public static List<ValidateResult> ValidateProject(Gp4Project proj, string projDir)
    {
      var ret = new List<ValidateResult>();

      foreach(var check in commonChecks)
      {
        var result = check(proj, projDir);
        if (result != null) ret.Add(result);
      }
      // Checks with project and SFO file
      if (proj.files.Items.Where(f => f.TargetPath == "sce_sys/param.sfo").FirstOrDefault() is Gp4File sfoFile
        && Path.Combine(projDir, sfoFile.OrigPath) is string sfoPath 
        && File.Exists(sfoPath))
      {
        ParamSfo sfoObject = null;
        try
        {
          using (var f = File.OpenRead(sfoPath))
          {
            sfoObject = ParamSfo.FromStream(f);
          }
        }
        catch (Exception e)
        {
          ret.Add(ValidateResult.Fatal("Could not load param.sfo file: " + e.Message));
        }
        if (sfoObject != null) foreach (var check in sfoChecks)
        {
          var result = check(proj, projDir, sfoObject);
          if (result != null) ret.Add(result);
        }
      }
      else
      {
        ret.Add(ValidateResult.Fatal("Required file sce_sys/param.sfo is missing."));
      }
      return ret;
    }
  }
}
