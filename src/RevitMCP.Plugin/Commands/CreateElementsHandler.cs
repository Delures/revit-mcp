using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Commands.Creation;
using RevitMCP.Plugin.Failures;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Scripting;
using RevitMCP.Shared.Utils;

namespace RevitMCP.Plugin.Commands
{
    public class CreateElementsHandler
    {
        private class WarningCollectorContext : IScriptExecutionContext
        {
            private readonly List<WarningRecord> _warnings = new List<WarningRecord>();
            private readonly List<CapturedDialogRecord> _dialogs = new List<CapturedDialogRecord>();
            private readonly List<string> _logs = new List<string>();
            private readonly List<long> _affected = new List<long>();

            public void Log(object? message) => _logs.Add(message?.ToString() ?? string.Empty);
            public void Print(object? message) => _logs.Add(message?.ToString() ?? string.Empty);
            public void TrackElement(long elementId) => _affected.Add(elementId);
            public void AddAffectedElements(IEnumerable<long> ids) { if (ids != null) _affected.AddRange(ids); }
            public void AddWarning(WarningRecord warning) => _warnings.Add(warning);
            public IReadOnlyList<WarningRecord> GetWarnings() => _warnings;
            public void AddCapturedDialog(CapturedDialogRecord dialog) => _dialogs.Add(dialog);
            public IReadOnlyList<CapturedDialogRecord> GetCapturedDialogs() => _dialogs;
            public IReadOnlyList<string> GetLogs() => _logs;
            public IReadOnlyList<long> GetAffectedElementIds() => _affected;
        }

        public CreateElementsResult Execute(UIApplication app, CreateElementsRequest request)
        {
            var result = new CreateElementsResult
            {
                Action = request.Action?.ToLowerInvariant() ?? "wall"
            };

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                result.Success = false;
                result.ErrorMessage = "No active document open.";
                return result;
            }

            var scriptContext = new WarningCollectorContext();

            try
            {
                string txName = HumanFriendlyNaming.ResolveTransactionName(request.TransactionName, result.Action, "create");
                using (var tx = new Transaction(doc, txName))
                {
                    var failOpt = tx.GetFailureHandlingOptions();
                    failOpt.SetFailuresPreprocessor(new SmartFailuresPreprocessor(scriptContext));
                    tx.SetFailureHandlingOptions(failOpt);
                    tx.Start();

                    (long id, string summary) created;

                    switch (result.Action)
                    {
                        case "level":
                            if (request.Level == null) throw new InvalidOperationException("Missing 'level' specification in request.");
                            created = DatumAndAnnotationCreator.CreateLevel(doc, request.Level);
                            break;

                        case "grid":
                            if (request.Grid == null) throw new InvalidOperationException("Missing 'grid' specification in request.");
                            created = DatumAndAnnotationCreator.CreateGrid(doc, request.Grid);
                            break;

                        case "room":
                            if (request.Room == null) throw new InvalidOperationException("Missing 'room' specification in request.");
                            created = DatumAndAnnotationCreator.CreateRoom(doc, request.Room);
                            break;

                        case "model_curve":
                            if (request.ModelCurve == null) throw new InvalidOperationException("Missing 'modelCurve' specification in request.");
                            created = DatumAndAnnotationCreator.CreateModelCurve(doc, request.ModelCurve);
                            break;

                        case "wall":
                            if (request.Wall == null) throw new InvalidOperationException("Missing 'wall' specification in request.");
                            created = WallCreator.CreateWall(doc, request.Wall);
                            break;

                        case "slab":
                        case "floor":
                            if (request.Slab == null) throw new InvalidOperationException("Missing 'slab' specification in request.");
                            created = SlabCreator.CreateSlab(doc, request.Slab);
                            break;

                        case "beam":
                            if (request.Beam == null) throw new InvalidOperationException("Missing 'beam' specification in request.");
                            created = StructuralFramingCreator.CreateBeam(doc, request.Beam);
                            break;

                        case "column":
                            if (request.Column == null) throw new InvalidOperationException("Missing 'column' specification in request.");
                            created = StructuralFramingCreator.CreateColumn(doc, request.Column);
                            break;

                        case "area_reinforcement":
                            if (request.AreaReinforcement == null) throw new InvalidOperationException("Missing 'areaReinforcement' specification in request.");
                            created = ReinforcementCreator.CreateAreaReinforcement(doc, request.AreaReinforcement);
                            break;

                        case "rebar":
                            if (request.Rebar == null) throw new InvalidOperationException("Missing 'rebar' specification in request.");
                            created = ReinforcementCreator.CreateRebar(doc, request.Rebar);
                            break;

                        case "family_instance":
                            if (request.FamilyInstance == null) throw new InvalidOperationException("Missing 'familyInstance' specification in request.");
                            created = FamilyInstanceCreator.CreateFamilyInstance(doc, request.FamilyInstance);
                            break;

                        default:
                            result.Success = false;
                            result.ErrorMessage = $"Unknown create action '{result.Action}'. Supported: level, grid, wall, family_instance, room, model_curve, slab, beam, column, area_reinforcement, rebar.";
                            tx.RollBack();
                            return result;
                    }

                    result.CreatedElementIds.Add(created.id);
                    result.ElementSummary = created.summary;

                    tx.Commit();
                }

                result.Warnings = scriptContext.GetWarnings().ToList();
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to create element ({result.Action}): {ex.Message}";
                return result;
            }
        }
    }
}
