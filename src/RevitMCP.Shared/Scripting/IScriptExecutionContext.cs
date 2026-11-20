using System.Collections.Generic;

namespace RevitMCP.Shared.Scripting
{
    public interface IScriptExecutionContext
    {
        void Print(object? message);
        void Log(object? message);
        IReadOnlyList<string> GetLogs();

        void AddAffectedElements(IEnumerable<long> elementIds);
        IReadOnlyList<long> GetAffectedElementIds();

        void AddWarning(WarningRecord warning);
        IReadOnlyList<WarningRecord> GetWarnings();

        void AddCapturedDialog(CapturedDialogRecord dialog);
        IReadOnlyList<CapturedDialogRecord> GetCapturedDialogs();
    }
}
