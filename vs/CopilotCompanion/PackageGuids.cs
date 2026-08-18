using System;

namespace CopilotCompanion
{
    /// <summary>GUIDs/IDs — must stay in sync with CopilotCompanionPackage.vsct.</summary>
    internal static class PackageGuids
    {
        public const string PackageGuidString = "b7a2c0e4-6d5a-4e8b-9f1c-3d2e8a7b4c01";
        public const string CmdSetGuidString = "d4f8e2a1-9c3b-4761-8e5d-0a6b9c2f7e12";

        public static readonly Guid CmdSet = new Guid(CmdSetGuidString);
    }

    internal static class PackageIds
    {
        public const int CopilotCompanionToolbar = 0x1000;
        public const int ToolsMenuGroup = 0x1020;
        public const int ToolbarGroup = 0x1050;

        public const int OpenCompanionCommandId = 0x0100;
        public const int RestoreLayoutCommandId = 0x0101;
        public const int ToggleFileSyncCommandId = 0x0102;
        public const int OpenCompanionToolWindowCommandId = 0x0103;
    }
}
