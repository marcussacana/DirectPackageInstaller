using System;

public enum DialogResult
{
    OK,
    Yes,
    No,
    Ignore,
    Cancel
}

[Flags]
public enum MessageBoxButtons
{
    OK = 1 << 0,
    Yes = 1 << 1,
    No  = 1 << 2,
    Retry = 1 << 3,
    Ignore = 1 << 4,
    Cancel = 1 << 5,
    YesNo = Yes | No,
    YesNoCancel = YesNo | Cancel,
    RetryCancel = Retry | Cancel,
    RetryIgnoreCancel = RetryCancel | Ignore,
    IgnoreCancel = Ignore | Cancel,
    RetryIgnore = Retry | Ignore
}

public enum MessageBoxIcon
{
    None,
    Error,
    Stop,
    Warning,
    Information,
    Question
}