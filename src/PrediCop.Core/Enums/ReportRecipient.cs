namespace PrediCop.Core.Enums;

[Flags]
public enum ReportRecipient
{
    None        = 0,
    Mayor       = 1,
    Prosecutor  = 2,
    Prefecture  = 4,
    Hierarchy   = 8,
    Department  = 16,
}
