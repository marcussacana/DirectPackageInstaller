using System;

namespace DirectPackageInstaller.Others;

public class AbortException : Exception
{
    public AbortException(string Message) : base(Message)
    {
        
    }
}