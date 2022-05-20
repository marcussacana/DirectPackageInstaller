using System.Linq;
//using WatsonWebserver;

namespace DirectPackageInstaller.Host
{
    struct HttpRange
    {
        public HttpRange(string Header)
        {
            var RangeStr = Header.Split('=').Last();
            var BeginStr = RangeStr.Split('-').First();
            var EndStr = RangeStr.Split('-').Last();

            if (string.IsNullOrWhiteSpace(EndStr) || EndStr == "*")
                EndStr = null;

            Begin = long.Parse(BeginStr);

            if (EndStr != null)
                End = long.Parse(EndStr);
            else
                End = null;
        }

        public long Begin;
        public long? End;
        public long? Length => (End - Begin) + 1;
    }
}
