using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Failures;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Utils;

namespace RevitMCP.Plugin.Commands
{
    public class VisualOverrideHandler
    {
        public VisualOverrideResult Execute(UIApplication app, VisualOverrideRequest request)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var uidoc = app.ActiveUIDocument;
            if (uidoc == null)
            {
                return new VisualOverrideResult
                {
                    Success = false,
                    ErrorMessage = "No active document found in Revit."
                };
            }

            var doc = uidoc.Document;

            // Resolve target view
            View view;
            if (request.ViewId.HasValue && request.ViewId.Value > 0)
            {
                var vElem = doc.GetElement(ElementIdExtensions.FromLongId(request.ViewId.Value));
                if (vElem is View v)
                {
                    view = v;
                }
                else
                {
                    return new VisualOverrideResult
                    {
                        Success = false,
                        ErrorMessage = $"View with ID {request.ViewId.Value} not found or is not a View."
                    };
                }
            }
            else
            {
                view = doc.ActiveView;
                if (view == null)
                {
                    return new VisualOverrideResult
                    {
                        Success = false,
                        ErrorMessage = "No active view found."
                    };
                }
            }

            string action = request.Action?.Trim().ToLowerInvariant() ?? "override_graphics";
            var targetIds = new List<ElementId>();
            if (request.ElementIds != null)
            {
                foreach (var id in request.ElementIds)
                {
                    var elem = doc.GetElement(ElementIdExtensions.FromLongId(id));
                    if (elem != null)
                    {
                        targetIds.Add(elem.Id);
                    }
                }
            }

            var result = new VisualOverrideResult
            {
                Action = action,
                ViewId = (int)view.Id.ToLongId(),
                ViewName = view.Name
            };

            // Temporary Hide/Isolate does not require transaction
            if (action == "isolate")
            {
                if (targetIds.Count == 0)
                {
                    return new VisualOverrideResult { Success = false, ErrorMessage = "No valid element IDs provided to isolate." };
                }
                view.IsolateElementsTemporary(targetIds);
                result.Success = true;
                result.AffectedElementIds = targetIds.Select(x => (int)x.ToLongId()).ToList();
                return result;
            }

            if (action == "reset_isolate" || action == "reset_temporary_isolate")
            {
                if (view.IsInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate))
                {
                    view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                }
                result.Success = true;
                return result;
            }

            // Other actions modify document view graphics and require transaction
            string txName = HumanFriendlyNaming.ResolveTransactionName(request.TransactionName, action, "visual_override");
            using var tx = new Transaction(doc, txName);
            var failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new SmartFailuresPreprocessor());
            tx.SetFailureHandlingOptions(failureOptions);

            tx.Start();

            try
            {
                switch (action)
                {
                    case "override_graphics":
                    {
                        var ogs = new OverrideGraphicSettings();

                        if (request.Transparency.HasValue)
                        {
                            int trans = Math.Max(0, Math.Min(100, request.Transparency.Value));
                            ogs.SetSurfaceTransparency(trans);
                        }

                        if (request.Halftone.HasValue)
                        {
                            ogs.SetHalftone(request.Halftone.Value);
                        }

                        if (request.Color != null)
                        {
                            var c = new Color(request.Color.R, request.Color.G, request.Color.B);
                            ogs.SetProjectionLineColor(c);
                            ogs.SetCutLineColor(c);

                            // Find a solid fill pattern if possible
                            var fillPatterns = new FilteredElementCollector(doc)
                                .OfClass(typeof(FillPatternElement))
                                .Cast<FillPatternElement>()
                                .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

                            if (fillPatterns != null)
                            {
                                ogs.SetSurfaceForegroundPatternId(fillPatterns.Id);
                                ogs.SetSurfaceForegroundPatternColor(c);
                                ogs.SetCutForegroundPatternId(fillPatterns.Id);
                                ogs.SetCutForegroundPatternColor(c);
                            }
                        }

                        foreach (var id in targetIds)
                        {
                            view.SetElementOverrides(id, ogs);
                        }

                        result.AffectedElementIds = targetIds.Select(x => (int)x.ToLongId()).ToList();
                        break;
                    }

                    case "reset_graphics":
                    {
                        var defaultOgs = new OverrideGraphicSettings();
                        foreach (var id in targetIds)
                        {
                            view.SetElementOverrides(id, defaultOgs);
                        }
                        result.AffectedElementIds = targetIds.Select(x => (int)x.ToLongId()).ToList();
                        break;
                    }

                    case "hide":
                    {
                        var validToHide = targetIds.Where(id =>
                        {
                            var elem = doc.GetElement(id);
                            return elem != null && elem.CanBeHidden(view);
                        }).ToList();

                        if (validToHide.Count > 0)
                        {
                            view.HideElements(validToHide);
                        }
                        result.AffectedElementIds = validToHide.Select(x => (int)x.ToLongId()).ToList();
                        break;
                    }

                    case "unhide":
                    {
                        if (targetIds.Count > 0)
                        {
                            view.UnhideElements(targetIds);
                        }
                        result.AffectedElementIds = targetIds.Select(x => (int)x.ToLongId()).ToList();
                        break;
                    }

                    case "set_rebar_presentation":
                    {
                        var affected = new List<int>();
                        View3D? view3D = view as View3D;

                        foreach (var id in targetIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem is Rebar rebar)
                            {
                                if (request.SolidIn3D.HasValue && view3D != null)
                                {
                                    // Use reflection to be safe across 2019..2027 where SetSolidInView might be deprecated or removed
                                    try
                                    {
                                        var method = typeof(Rebar).GetMethod("SetSolidInView", new[] { typeof(View3D), typeof(bool) });
                                        method?.Invoke(rebar, new object[] { view3D, request.SolidIn3D.Value });
                                    }
                                    catch { }
                                }

                                if (request.UnobscuredInView.HasValue)
                                {
                                    try
                                    {
                                        rebar.SetUnobscuredInView(view, request.UnobscuredInView.Value);
                                    }
                                    catch { }
                                }

                                affected.Add((int)rebar.Id.ToLongId());
                            }
                            else if (elem is AreaReinforcement areaRebar)
                            {
                                if (request.SolidIn3D.HasValue && view3D != null)
                                {
                                    try
                                    {
                                        var method = typeof(AreaReinforcement).GetMethod("SetSolidInView", new[] { typeof(View3D), typeof(bool) });
                                        method?.Invoke(areaRebar, new object[] { view3D, request.SolidIn3D.Value });
                                    }
                                    catch { }
                                }

                                if (request.UnobscuredInView.HasValue)
                                {
                                    try
                                    {
                                        areaRebar.SetUnobscuredInView(view, request.UnobscuredInView.Value);
                                    }
                                    catch { }
                                }

                                affected.Add((int)areaRebar.Id.ToLongId());
                            }
                        }

                        result.AffectedElementIds = affected;
                        break;
                    }

                    default:
                        tx.RollBack();
                        return new VisualOverrideResult { Success = false, ErrorMessage = $"Unknown action '{action}'." };
                }

                tx.Commit();
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                if (tx.HasStarted() && !tx.HasEnded())
                {
                    tx.RollBack();
                }
                return new VisualOverrideResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
