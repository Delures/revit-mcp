using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Plugin.Failures
{
    public class SmartFailuresPreprocessor : IFailuresPreprocessor
    {
        private readonly IScriptExecutionContext? _context;

        public SmartFailuresPreprocessor(IScriptExecutionContext? context = null)
        {
            _context = context;
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failureMessages = failuresAccessor.GetFailureMessages();
            if (failureMessages == null || failureMessages.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }

            bool hasUnresolvedErrors = false;

            foreach (var failure in failureMessages)
            {
                FailureSeverity severity = failure.GetSeverity();
                string message = failure.GetDescriptionText();
                string defId = failure.GetFailureDefinitionId().Guid.ToString();
                var failingIds = failure.GetFailingElementIds()
                    .Select(id => id.ToLongId())
                    .ToList();

                if (severity == FailureSeverity.Warning)
                {
                    // Non-critical warning (e.g. Duplicate Mark, wall overlap, off-axis, room tag outside):
                    // Automatically delete warning to prevent modal dialog popup in Revit!
                    _context?.AddWarning(new WarningRecord
                    {
                        Severity = "Warning",
                        DefinitionId = defId,
                        Message = message,
                        ElementIds = failingIds,
                        ActionTaken = "AutoSuppressed"
                    });

                    failuresAccessor.DeleteWarning(failure);
                }
                else if (severity == FailureSeverity.Error)
                {
                    if (failure.HasResolutions())
                    {
                        failuresAccessor.ResolveFailure(failure);
                        _context?.AddWarning(new WarningRecord
                        {
                            Severity = "Error",
                            DefinitionId = defId,
                            Message = message,
                            ElementIds = failingIds,
                            ActionTaken = "Resolved"
                        });
                    }
                    else
                    {
                        _context?.AddWarning(new WarningRecord
                        {
                            Severity = "Error",
                            DefinitionId = defId,
                            Message = message,
                            ElementIds = failingIds,
                            ActionTaken = "Rollback"
                        });
                        hasUnresolvedErrors = true;
                    }
                }
            }

            if (hasUnresolvedErrors)
            {
                return FailureProcessingResult.ProceedWithRollBack;
            }

            return FailureProcessingResult.Continue;
        }
    }
}
