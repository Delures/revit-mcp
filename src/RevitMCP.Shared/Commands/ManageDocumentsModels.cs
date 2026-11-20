using System.Collections.Generic;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Shared.Commands
{
    public class ManageDocumentsRequest : IAgentAction
    {
        public string Action { get; set; } = "list"; // "list", "open", "save", "save_as", "close", "sync_with_central"
        public string? FilePath { get; set; }
        public bool? Save { get; set; }
        public string? Comment { get; set; }
        public bool ShowUi { get; set; } = true;

        // "open" options (backward compatible — all optional)
        public string? DetachMode { get; set; }          // "none" (default) | "preserve_worksets" | "discard_worksets"
        public object? Worksets { get; set; }            // "all" (default) | "none" | "editable" | string[] names/prefixes
        public bool Audit { get; set; }                  // OpenOptions.Audit
        public bool? SuppressDialogs { get; set; }       // default true when detachMode != "none"

        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
    }

    public class DocumentInfo
    {
        public string Title { get; set; } = string.Empty;
        public string PathName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsWorkshared { get; set; }
        public bool IsModified { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsFamilyDocument { get; set; }
    }

    public class ManageDocumentsResult
    {
        public bool Success { get; set; } = true;
        public string Action { get; set; } = string.Empty;
        public DocumentInfo? ActiveDocument { get; set; }
        public List<DocumentInfo> Documents { get; set; } = new List<DocumentInfo>();
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }

        /// <summary>Dialogs suppressed during open/close (e.g. Dialog_Revit_DocWarnDialog, TaskDialog_Unresolved_References).</summary>
        public List<CapturedDialogRecord> SuppressedDialogs { get; set; } = new List<CapturedDialogRecord>();

        /// <summary>Failures (warnings) suppressed during open/close via FailuresProcessing handler.</summary>
        public List<WarningRecord> SuppressedWarnings { get; set; } = new List<WarningRecord>();
    }
}
