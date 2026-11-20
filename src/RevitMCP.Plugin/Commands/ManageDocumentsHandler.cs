using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitMCP.Plugin.Failures;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Plugin.Commands
{
    public class ManageDocumentsHandler
    {
        public ManageDocumentsResult Execute(UIApplication app, ManageDocumentsRequest request)
        {
            var result = new ManageDocumentsResult
            {
                Action = request.Action?.ToLowerInvariant() ?? "list"
            };

            try
            {
                switch (result.Action)
                {
                    case "list":
                        ExecuteList(app, result);
                        break;

                    case "open":
                        ExecuteOpen(app, request, result);
                        break;

                    case "save":
                        ExecuteSave(app, result);
                        break;

                    case "save_as":
                        ExecuteSaveAs(app, request, result);
                        break;

                    case "close":
                        ExecuteClose(app, request, result);
                        break;

                    case "sync_with_central":
                        ExecuteSyncWithCentral(app, request, result);
                        break;

                    default:
                        result.Success = false;
                        result.ErrorCode = "INVALID_ACTION";
                        result.Message = $"Unknown action '{request.Action}'. Supported actions: list, open, save, save_as, close, sync_with_central.";
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorCode = "DOCUMENT_ERROR";
                result.Message = $"{ex.GetType().Name}: {ex.Message}";
            }

            return result;
        }

        private static void ExecuteList(UIApplication app, ManageDocumentsResult result)
        {
            var activeDoc = app.ActiveUIDocument?.Document;
            var docList = new List<DocumentInfo>();

            foreach (Document doc in app.Application.Documents)
            {
                bool isActive = activeDoc != null && (doc.Equals(activeDoc) || doc.Title == activeDoc.Title);
                var info = new DocumentInfo
                {
                    Title = doc.Title,
                    PathName = doc.PathName ?? string.Empty,
                    IsActive = isActive,
                    IsWorkshared = doc.IsWorkshared,
                    IsModified = doc.IsModified,
                    IsReadOnly = doc.IsReadOnly,
                    IsFamilyDocument = doc.IsFamilyDocument
                };

                docList.Add(info);
                if (isActive)
                {
                    result.ActiveDocument = info;
                }
            }

            result.Documents = docList;
            result.Success = true;
            result.Message = $"Found {docList.Count} open document(s).";
        }

        private static void ExecuteOpen(UIApplication app, ManageDocumentsRequest request, ManageDocumentsResult result)
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                result.Success = false;
                result.ErrorCode = "MISSING_FILEPATH";
                result.Message = "filePath parameter is required to open a document.";
                return;
            }

            string fullPath = Path.GetFullPath(request.FilePath);
            if (!File.Exists(fullPath))
            {
                result.Success = false;
                result.ErrorCode = "FILE_NOT_FOUND";
                result.Message = $"File not found at path: {fullPath}";
                return;
            }

            // Resolve detach mode
            string detachMode = request.DetachMode?.ToLowerInvariant() ?? "none";
            bool detach = detachMode != "none";
            DetachFromCentralOption detachOption = detachMode == "discard_worksets"
                ? DetachFromCentralOption.DetachAndDiscardWorksets
                : DetachFromCentralOption.DetachAndPreserveWorksets;

            // Suppress dialogs by default when detaching, or when explicitly requested
            bool suppress = request.SuppressDialogs ?? detach;

            ModelPath modelPath;
            try
            {
                modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(fullPath);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorCode = "INVALID_PATH";
                result.Message = $"Failed to convert path to ModelPath: {ex.Message}";
                return;
            }

            var openOptions = new OpenOptions();
            if (detach)
            {
                openOptions.DetachFromCentralOption = detachOption;
            }
            openOptions.Audit = request.Audit;

            // Resolve workset configuration
            var worksetConfig = BuildWorksetConfiguration(request.Worksets, modelPath);
            if (worksetConfig != null)
            {
                openOptions.SetOpenWorksetsConfiguration(worksetConfig);
            }

            var suppressedWarnings = new List<WarningRecord>();
            using (var dialogManager = new SmartDialogManager(app))
            {
                if (suppress)
                {
                    dialogManager.BeginSuppression();
                }

                // Temporary FailuresProcessing to clear warnings during open (verified on a real project file)
                EventHandler<Autodesk.Revit.DB.Events.FailuresProcessingEventArgs>? failuresHandler = null;
                if (suppress)
                {
                    failuresHandler = (_, e) =>
                    {
                        var fa = e.GetFailuresAccessor();
                        foreach (var fm in fa.GetFailureMessages())
                        {
                            if (fm.GetSeverity() == FailureSeverity.Warning)
                            {
                                suppressedWarnings.Add(new WarningRecord
                                {
                                    Severity = "Warning",
                                    DefinitionId = fm.GetFailureDefinitionId().Guid.ToString(),
                                    Message = fm.GetDescriptionText(),
                                    ElementIds = fm.GetFailingElementIds().Select(id => id.ToLongId()).ToList(),
                                    ActionTaken = "AutoSuppressed"
                                });
                                fa.DeleteWarning(fm);
                            }
                        }
                    };
                    app.Application.FailuresProcessing += failuresHandler;
                }

                try
                {
                    Document openedDoc;
                    if (request.ShowUi)
                    {
                        var uiDoc = app.OpenAndActivateDocument(modelPath, openOptions, false);
                        openedDoc = uiDoc.Document;
                    }
                    else
                    {
                        openedDoc = app.Application.OpenDocumentFile(modelPath, openOptions);
                    }

                    result.ActiveDocument = new DocumentInfo
                    {
                        Title = openedDoc.Title,
                        PathName = openedDoc.PathName ?? fullPath,
                        IsActive = true,
                        IsWorkshared = openedDoc.IsWorkshared,
                        IsModified = openedDoc.IsModified,
                        IsReadOnly = openedDoc.IsReadOnly,
                        IsFamilyDocument = openedDoc.IsFamilyDocument
                    };

                    result.Success = true;
                    result.Message = $"Successfully opened document '{openedDoc.Title}'.";
                }
                finally
                {
                    if (failuresHandler != null)
                    {
                        app.Application.FailuresProcessing -= failuresHandler;
                    }
                    if (suppress)
                    {
                        dialogManager.EndSuppression();
                    }
                }

                result.SuppressedDialogs = dialogManager.GetCapturedDialogs().ToList();
            }

            result.SuppressedWarnings = suppressedWarnings;
        }

        private static WorksetConfiguration? BuildWorksetConfiguration(object? worksets, ModelPath modelPath)
        {
            if (worksets == null)
            {
                return null; // Revit default (all worksets open)
            }

            // Collect requested patterns (names/prefixes/substrings) from string shorthand or list
            var patterns = new List<string>();

            if (worksets is string str)
            {
                switch (str.ToLowerInvariant())
                {
                    case "all":
                        return new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets);
                    case "none":
                        return new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                    case "editable":
                        return new WorksetConfiguration(WorksetConfigurationOption.OpenLastViewed);
                    default:
                        // Treat as a single workset name/prefix/substring
                        patterns.Add(str);
                        break;
                }
            }
            else if (worksets is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        patterns.Add(item.GetString()!);
                    }
                }
            }
            else if (worksets is IEnumerable<string> strList)
            {
                patterns.AddRange(strList);
            }
            else if (worksets is IEnumerable<object> objList)
            {
                patterns.AddRange(objList.Select(o => o?.ToString() ?? string.Empty));
            }

            if (patterns.Count == 0)
            {
                return new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets);
            }

            // Filter workset ids by pattern (name/prefix/substring, case-insensitive) using the
            // pre-open preview API WorksharingUtils.GetUserWorksetInfo(ModelPath).
            try
            {
                IList<WorksetPreview> previews = WorksharingUtils.GetUserWorksetInfo(modelPath);
                if (previews == null || previews.Count == 0)
                {
                    return null; // not a workshared model -> let Revit open it as non-workshared
                }

                var matchedIds = new List<WorksetId>();
                foreach (var wp in previews)
                {
                    if (string.IsNullOrEmpty(wp.Name))
                    {
                        continue;
                    }

                    foreach (var pattern in patterns)
                    {
                        if (string.IsNullOrEmpty(pattern))
                        {
                            continue;
                        }

                        if (wp.Name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedIds.Add(wp.Id);
                            break;
                        }
                    }
                }

                var config = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
                if (matchedIds.Count > 0)
                {
                    config.Open(matchedIds);
                }
                return config;
            }
            catch
            {
                return null; // workset info unavailable -> fall back to Revit default open
            }
        }

        private static void ExecuteSave(UIApplication app, ManageDocumentsResult result)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                result.Success = false;
                result.ErrorCode = "NO_ACTIVE_DOCUMENT";
                result.Message = "No active document open to save.";
                return;
            }

            if (doc.IsReadOnly)
            {
                result.Success = false;
                result.ErrorCode = "READ_ONLY";
                result.Message = "Document is read-only and cannot be overwritten directly. Use save_as instead.";
                return;
            }

            doc.Save();
            result.Success = true;
            result.Message = $"Document '{doc.Title}' saved successfully.";
        }

        private static void ExecuteSaveAs(UIApplication app, ManageDocumentsRequest request, ManageDocumentsResult result)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                result.Success = false;
                result.ErrorCode = "NO_ACTIVE_DOCUMENT";
                result.Message = "No active document open to save.";
                return;
            }

            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                result.Success = false;
                result.ErrorCode = "MISSING_FILEPATH";
                result.Message = "filePath parameter is required for save_as action.";
                return;
            }

            string targetPath = Path.GetFullPath(request.FilePath);
            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var saveAsOpts = new SaveAsOptions
            {
                OverwriteExistingFile = true
            };

            doc.SaveAs(targetPath, saveAsOpts);
            result.Success = true;
            result.Message = $"Document '{doc.Title}' saved to '{targetPath}'.";
        }

        private static void ExecuteClose(UIApplication app, ManageDocumentsRequest request, ManageDocumentsResult result)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                result.Success = false;
                result.ErrorCode = "NO_ACTIVE_DOCUMENT";
                result.Message = "No active document open to close.";
                return;
            }

            string title = doc.Title;
            bool doSave = request.Save ?? false;

            doc.Close(doSave);
            result.Success = true;
            result.Message = $"Document '{title}' closed (saved: {doSave}).";
        }

        private static void ExecuteSyncWithCentral(UIApplication app, ManageDocumentsRequest request, ManageDocumentsResult result)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                result.Success = false;
                result.ErrorCode = "NO_ACTIVE_DOCUMENT";
                result.Message = "No active document open to sync.";
                return;
            }

            if (!doc.IsWorkshared)
            {
                result.Success = false;
                result.ErrorCode = "NOT_WORKSHARED";
                result.Message = "Active document is not a workshared model (cannot synchronize with central).";
                return;
            }

            var transactOpts = new TransactWithCentralOptions();
            var syncOpts = new SynchronizeWithCentralOptions();

            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                syncOpts.Comment = request.Comment;
            }

            // Relinquish options
            var relinquishOpts = new RelinquishOptions(true);
            syncOpts.SetRelinquishOptions(relinquishOpts);
            syncOpts.SaveLocalAfter = true;

            doc.SynchronizeWithCentral(transactOpts, syncOpts);
            result.Success = true;
            result.Message = $"Successfully synchronized '{doc.Title}' with central model.";
        }
    }
}
