using System;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Failures;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Plugin.Scripting
{
    public class RevitScriptContext : IRevitScriptContext
    {
        private readonly UIApplication _app;
        private readonly List<string> _logs = new List<string>();
        private readonly List<long> _affectedElementIds = new List<long>();
        private readonly List<WarningRecord> _warnings = new List<WarningRecord>();
        private readonly List<CapturedDialogRecord> _capturedDialogs = new List<CapturedDialogRecord>();

        public RevitScriptContext(UIApplication app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public UIApplication App => _app;
        public Application Application => _app.Application;
        public UIDocument? UIDoc => _app.ActiveUIDocument;
        public Document? Doc => _app.ActiveUIDocument?.Document;

        public T WithTransaction<T>(string transactionName, Func<T> action)
        {
            if (Doc == null)
            {
                throw new InvalidOperationException("Active document is null. Cannot start a transaction without an open document.");
            }

            using (var trans = new Transaction(Doc, transactionName))
            {
                // Attach SmartFailuresPreprocessor to automatically suppress benign warnings (e.g. Duplicate Mark)
                var failureOptions = trans.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new SmartFailuresPreprocessor(this));
                failureOptions.SetClearAfterRollback(true);
                trans.SetFailureHandlingOptions(failureOptions);

                trans.Start();
                try
                {
                    T result = action();
                    trans.Commit();
                    return result;
                }
                catch
                {
                    if (trans.HasStarted() && !trans.HasEnded())
                    {
                        trans.RollBack();
                    }
                    throw;
                }
            }
        }

        public void WithTransaction(string transactionName, Action action)
        {
            WithTransaction(transactionName, () =>
            {
                action();
                return true;
            });
        }

        public T WithTransactionGroup<T>(string groupName, Func<T> action)
        {
            if (Doc == null)
            {
                throw new InvalidOperationException("Active document is null. Cannot start a transaction group without an open document.");
            }

            using (var group = new TransactionGroup(Doc, groupName))
            {
                group.Start();
                try
                {
                    T result = action();
                    group.Assimilate();
                    return result;
                }
                catch
                {
                    if (group.HasStarted() && !group.HasEnded())
                    {
                        group.RollBack();
                    }
                    throw;
                }
            }
        }

        public void WithTransactionGroup(string groupName, Action action)
        {
            WithTransactionGroup(groupName, () =>
            {
                action();
                return true;
            });
        }

        public void Print(object? message)
        {
            string text = message?.ToString() ?? "null";
            _logs.Add(text);
        }

        public void Log(object? message) => Print(message);

        public IReadOnlyList<string> GetLogs()
        {
            return _logs.AsReadOnly();
        }

        public void AddAffectedElements(IEnumerable<ElementId> elementIds)
        {
            if (elementIds == null) return;
            foreach (var id in elementIds)
            {
                _affectedElementIds.Add(id.ToLongId());
            }
        }

        public void AddAffectedElements(IEnumerable<long> elementIds)
        {
            if (elementIds == null) return;
            _affectedElementIds.AddRange(elementIds);
        }

        public IReadOnlyList<long> GetAffectedElementIds()
        {
            return _affectedElementIds.AsReadOnly();
        }

        public void AddWarning(WarningRecord warning)
        {
            if (warning != null) _warnings.Add(warning);
        }

        public IReadOnlyList<WarningRecord> GetWarnings()
        {
            return _warnings.AsReadOnly();
        }

        public void AddCapturedDialog(CapturedDialogRecord dialog)
        {
            if (dialog != null) _capturedDialogs.Add(dialog);
        }

        public IReadOnlyList<CapturedDialogRecord> GetCapturedDialogs()
        {
            return _capturedDialogs.AsReadOnly();
        }
    }
}
