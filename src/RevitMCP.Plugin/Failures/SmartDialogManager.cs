using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Plugin.Failures
{
    public class SmartDialogManager : IDisposable
    {
        private readonly UIApplication _app;
        private IScriptExecutionContext? _activeContext;
        private bool _isHooked;
        private bool _suppressAlways;
        private readonly List<CapturedDialogRecord> _capturedDialogs = new List<CapturedDialogRecord>();

        public SmartDialogManager(UIApplication app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            Hook();
        }

        public void SetActiveContext(IScriptExecutionContext? context)
        {
            _activeContext = context;
        }

        /// <summary>
        /// Begin unconditional suppression (used by manage_documents open/close, where no
        /// script context exists but we still must suppress open-file dialogs).
        /// </summary>
        public void BeginSuppression()
        {
            _suppressAlways = true;
        }

        public void EndSuppression()
        {
            _suppressAlways = false;
        }

        /// <summary>All dialogs captured since construction (regardless of active context).</summary>
        public IReadOnlyList<CapturedDialogRecord> GetCapturedDialogs()
        {
            return _capturedDialogs;
        }

        private void Hook()
        {
            if (!_isHooked)
            {
                _app.DialogBoxShowing += OnDialogBoxShowing;
                _isHooked = true;
            }
        }

        private void Unhook()
        {
            if (_isHooked)
            {
                _app.DialogBoxShowing -= OnDialogBoxShowing;
                _isHooked = false;
            }
        }

        private void OnDialogBoxShowing(object? sender, DialogBoxShowingEventArgs e)
        {
            // Interfere only when a script execution is active OR suppression was explicitly requested.
            if (_activeContext == null && !_suppressAlways) return;

            string dialogId = e.DialogId ?? "UnknownDialog";
            string message = string.Empty;
            string actionTaken = "ClickedOk";

            if (e is TaskDialogShowingEventArgs td)
            {
                message = td.Message ?? string.Empty;

                // "Unresolved references" warning dialog on open -> "Ignore and continue" (CommandLink2),
                // NOT Cancel, otherwise the open operation is aborted.
                if (dialogId.IndexOf("Unresolved_References", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dialogId.IndexOf("UnresolvedReferences", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    td.OverrideResult((int)TaskDialogResult.CommandLink2);
                    actionTaken = "OverrideResult_CommandLink2_IgnoreAndContinue";
                }
                // Family reload: "Overwrite the existing version" is usually CommandLink1
                else if (dialogId.IndexOf("Family", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         message.IndexOf("overwrite", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    td.OverrideResult((int)TaskDialogResult.CommandLink1);
                    actionTaken = "OverrideResult_CommandLink1_Overwrite";
                }
                else
                {
                    td.OverrideResult((int)TaskDialogResult.Ok);
                    actionTaken = "OverrideResult_Ok";
                }
            }
            else
            {
                // Win32 dialogs. IDOK = 1. Revit's document-warning dialog (Dialog_Revit_DocWarnDialog)
                // needs OverrideResult(6) to confirm "open anyway" — otherwise open is cancelled
                // with OperationCanceledException ("Opening was canceled").
                if (dialogId.IndexOf("DocWarnDialog", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    e.OverrideResult(6);
                    actionTaken = "OverrideResult_DocWarnDialog_OpenAnyway";
                }
                else
                {
                    e.OverrideResult(1);
                    actionTaken = "OverrideResult_Win32_IDOK";
                }
            }

            var record = new CapturedDialogRecord
            {
                DialogId = dialogId,
                Message = message,
                ActionTaken = actionTaken,
                Timestamp = DateTime.UtcNow
            };

            _capturedDialogs.Add(record);
            _activeContext?.AddCapturedDialog(record);
        }

        public void Dispose()
        {
            Unhook();
        }
    }
}
