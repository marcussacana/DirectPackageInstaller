using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using DirectPackageInstaller.ViewModels;
using LibOrbisPkg.PKG;
using LibOrbisPkg.SFO;

namespace DirectPackageInstaller;

public static class PKGHelper
{
    public static PKGInfo? GetPKGInfo(this Stream Input)
    {
        try
        {
            PKGInfo Result = new PKGInfo();
            var PKGParser = new PkgReader(Input);
            var PKG = PKGParser.ReadPkg();

            Result.Digest = string.Join("", PKG.HeaderDigest.Select((x) => x.ToString("X2")));

            Result.PackageSize = Input.Length;
            
            Result.Entries = PKG.Metas.Metas.Select(x => (x.DataOffset, x.DataOffset + x.DataSize, x.DataSize, x.id)).ToArray();

            var SystemVer = PKG.ParamSfo.ParamSfo.HasName("SYSTEM_VER") ? PKG.ParamSfo.ParamSfo["SYSTEM_VER"].ToByteArray() : new byte[4];
            
            Result.FriendlyName = Encoding.UTF8.GetString(PKG.ParamSfo.ParamSfo.HasName("TITLE") ? PKG.ParamSfo.ParamSfo["TITLE"].ToByteArray() : new byte[0]).Trim('\x0');

            Result.FakePackage = PKG.CheckPasscode("00000000000000000000000000000000");
            Result.Description = $"[{SystemVer[3]:X2}.{SystemVer[2]:X2} - {(Result.FakePackage ? "Fake" : "Retail")}] {Result.FriendlyName}";

            try
            {

                Result.Params = new List<PkgParamInfo>();

                foreach (var Param in PKG.ParamSfo.ParamSfo.Values)
                {
                    var Name = Param.Name;
                    var RawValue = Param.ToByteArray();

                    bool DecimalValue = new[] {"APP_TYPE", "PARENTAL_LEVEL", "DEV_FLAG"}.Contains(Name);

                    var Value = Param.Type switch
                    {
                        SfoEntryType.Utf8Special => "",
                        SfoEntryType.Utf8 => Encoding.UTF8.GetString(RawValue).Trim('\x0'),
                        SfoEntryType.Integer => BitConverter.ToUInt32(RawValue, 0).ToString(DecimalValue ? "D1" : "X8"),
                        _ => throw new NotImplementedException(),
                    };

                    if (string.IsNullOrWhiteSpace(Value))
                        continue;
                    
                    if (Name == "CATEGORY")
                        Result.ContentType = Value;

                    if (Name == "CONTENT_ID")
                        Result.ContentID = Value;

                    if (Name == "TITLE_ID")
                        Result.TitleID = Value;

                    Result.Params.Add(new PkgParamInfo() {Name = Name, Value = Value});
                }
            }
            catch
            {
            }

            if (PKG.Metas.Metas.Where(entry => entry.id == EntryId.ICON0_PNG).FirstOrDefault() is MetaEntry Icon)
            {
                try
                {
                    Input.Position = Icon.DataOffset;
                    byte[] Buffer = new byte[Icon.DataSize];
                    Input.Read(Buffer, 0, Buffer.Length);

                    Result.IconData = Buffer;
                }
                catch
                {
                }
            }

            return Result;
        }
        catch
        {
            return null;
        }
    }
    public struct PKGInfo
    {
        public string FriendlyName;
        public string ContentID;
        public string TitleID;
        public byte[] IconData;

        public bool FakePackage;
        public string Description;

        public string Digest;

        public string ContentType;

        public long PackageSize;

        public string BGFTContentType => $"PS4{ContentType.ToUpperInvariant()}";
        public string FirendlyContentType
        {
            get => ContentType.ToLowerInvariant().Trim() switch {
                "ac" => "Additional Content",
                "bd" => "Blu-ray Disc",
                "gc" => "Game Content",
                "gd" => "Game Digital Application",
                "gda" => "System Application",
                "gdc" => "Non-Game Big Application",
                "gdd" => "BG Application",
                "gde" => "Non-Game Mini App / Video Service Native App",
                "gdk" => "Video Service Web App",
                "gdl" => "PS Cloud Beta App",
                "gdo" => "PS2 Classic",
                "gp" => "Game Application Patch",
                "gpc" => "Non-Game Big App Patch",
                "gpd" => "BG Application Patch",
                "gpe" => "Non-Game Mini App Patch / Video Service Native App Patch",
                "gpk" => "Video Service Web App Patch",
                "gpl" => "PS Cloud Beta App Patch",
                "sd" => "Save Data",
                _ => "???"
            } + $" ({ContentType})";
        }
        public Bitmap? Icon
        {
            get
            {
                if (IconData == null || IconData.Length == 0)
                    return null;
                try
                {
                    using (Stream ImgBuffer = new MemoryStream(IconData))
                    {
                        return Bitmap.DecodeToHeight(ImgBuffer, 512);
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        public (uint Offset, uint End, uint Size, EntryId Id)[] Entries;

        public List<PkgParamInfo> Params;
    }
    
    
}