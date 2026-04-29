using System;

namespace XamlGridExtension;

internal static class PackageGuids
{
    public const string XamlGridExtensionPackageString = "6B1A3A2F-8C4E-4D1B-A9F2-3C5E7D8B2F1A";
    public static readonly Guid XamlGridExtensionPackage = new(XamlGridExtensionPackageString);

    public const string XamlGridExtensionCmdSetString = "A2B3C4D5-E6F7-4A8B-9C0D-1E2F3A4B5C6D";
    public static readonly Guid XamlGridExtensionCmdSet = new(XamlGridExtensionCmdSetString);
}

internal static class PackageIds
{
    public const int InsertRowCommandId    = 0x0100;
    public const int RemoveRowCommandId    = 0x0200;
    public const int InsertColumnCommandId = 0x0300;
    public const int RemoveColumnCommandId = 0x0400;
    public const int GridToolWindowId      = 0x0001;
}
