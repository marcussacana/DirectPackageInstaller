using System.IO;
using DirectPackageInstaller.IO;
//using WatsonWebserver;
using SharpCompress.Archives;

namespace DirectPackageInstaller.Host
{
    struct ClientInfo
    {
        public ClientInfo(FileHostStream HttpStream, Stream Unrar, IArchive Archive, long Errors, long LastPos)
        {
            this.HttpStream = HttpStream;
            this.Unrar = Unrar;
            this.Archive = Archive;
            this.Errors = Errors;
            this.LastPos = LastPos;
        }

        public FileHostStream HttpStream;
        public Stream Unrar;
        public IArchive Archive;
        public long Errors;
        public long LastPos;
    }
}
