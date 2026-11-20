using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Failures;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Scripting;
using RevitMCP.Shared.Units;
using RevitMCP.Shared.Utils;

namespace RevitMCP.Plugin.Commands
{
    public class TransformElementsHandler
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

        public TransformElementsResult Execute(UIApplication app, TransformElementsRequest request)
        {
            var result = new TransformElementsResult
            {
                Action = request.Action?.ToLowerInvariant() ?? "move"
            };

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                result.Success = false;
                result.ErrorMessage = "No active document open.";
                return result;
            }

            if (request.ElementIds == null || request.ElementIds.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "elementIds list cannot be empty.";
                return result;
            }

            var validElementIds = new List<ElementId>();
            foreach (var id in request.ElementIds)
            {
                var eid = ElementIdExtensions.FromLongId(id);
                var elem = doc.GetElement(eid);
                if (elem != null)
                {
                    validElementIds.Add(eid);
                }
            }

            if (validElementIds.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "None of the specified elementIds exist in the document.";
                return result;
            }

            var scriptContext = new WarningCollectorContext();

            try
            {
                string txName = HumanFriendlyNaming.ResolveTransactionName(request.TransactionName, result.Action, "transform");
                using (var tx = new Transaction(doc, txName))
                {
                    var failOpt = tx.GetFailureHandlingOptions();
                    failOpt.SetFailuresPreprocessor(new SmartFailuresPreprocessor(scriptContext));
                    tx.SetFailureHandlingOptions(failOpt);
                    tx.Start();

                    switch (result.Action)
                    {
                        case "move":
                        {
                            var dxFeet = RevitUnitConverter.MmToFeet(request.Dx);
                            var dyFeet = RevitUnitConverter.MmToFeet(request.Dy);
                            var dzFeet = RevitUnitConverter.MmToFeet(request.Dz);
                            var vec = new XYZ(dxFeet, dyFeet, dzFeet);

                            ElementTransformUtils.MoveElements(doc, validElementIds, vec);
                            result.AffectedElementIds = validElementIds.Select(ElementIdExtensions.ToLongId).ToList();
                            result.Count = result.AffectedElementIds.Count;
                            break;
                        }

                        case "rotate":
                        {
                            var angleRad = RevitUnitConverter.DegreesToRadians(request.AngleDegrees);

                            XYZ origin;
                            if (request.AxisOrigin != null)
                            {
                                origin = new XYZ(
                                    RevitUnitConverter.MmToFeet(request.AxisOrigin.X),
                                    RevitUnitConverter.MmToFeet(request.AxisOrigin.Y),
                                    RevitUnitConverter.MmToFeet(request.AxisOrigin.Z));
                            }
                            else
                            {
                                // Default to center of bounding box of first element
                                var firstElem = doc.GetElement(validElementIds[0]);
                                var bbox = firstElem?.get_BoundingBox(null);
                                if (bbox != null)
                                {
                                    origin = (bbox.Min + bbox.Max) * 0.5;
                                }
                                else
                                {
                                    origin = XYZ.Zero;
                                }
                            }

                            XYZ dir;
                            if (request.AxisDirection != null)
                            {
                                dir = new XYZ(request.AxisDirection.X, request.AxisDirection.Y, request.AxisDirection.Z);
                                if (dir.IsAlmostEqualTo(XYZ.Zero))
                                {
                                    dir = XYZ.BasisZ;
                                }
                            }
                            else
                            {
                                dir = XYZ.BasisZ;
                            }

                            var axisLine = Line.CreateUnbound(origin, dir.Normalize());
                            ElementTransformUtils.RotateElements(doc, validElementIds, axisLine, angleRad);
                            result.AffectedElementIds = validElementIds.Select(ElementIdExtensions.ToLongId).ToList();
                            result.Count = result.AffectedElementIds.Count;
                            break;
                        }

                        case "copy":
                        {
                            var dxFeet = RevitUnitConverter.MmToFeet(request.Dx);
                            var dyFeet = RevitUnitConverter.MmToFeet(request.Dy);
                            var dzFeet = RevitUnitConverter.MmToFeet(request.Dz);
                            var vec = new XYZ(dxFeet, dyFeet, dzFeet);

                            var newIds = ElementTransformUtils.CopyElements(doc, validElementIds, vec);
                            result.AffectedElementIds = validElementIds.Select(ElementIdExtensions.ToLongId).ToList();
                            result.NewElementIds = newIds.Select(ElementIdExtensions.ToLongId).ToList();
                            result.Count = result.NewElementIds.Count;
                            break;
                        }

                        case "delete":
                        {
                            var deletedIds = doc.Delete(validElementIds);
                            result.AffectedElementIds = deletedIds.Select(ElementIdExtensions.ToLongId).ToList();
                            result.Count = result.AffectedElementIds.Count;
                            break;
                        }

                        default:
                            result.Success = false;
                            result.ErrorMessage = $"Unknown transform action '{result.Action}'. Supported: move, rotate, copy, delete.";
                            tx.RollBack();
                            return result;
                    }

                    tx.Commit();
                }

                result.Warnings = scriptContext.GetWarnings().ToList();
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to execute transform {result.Action}: {ex.Message}";
                return result;
            }
        }
    }
}
