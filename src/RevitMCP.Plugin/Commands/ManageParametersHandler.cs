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
    public class ManageParametersHandler
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

        public ManageParametersResult Execute(UIApplication app, ManageParametersRequest request)
        {
            var result = new ManageParametersResult
            {
                Action = request.Action?.ToLowerInvariant() ?? "get"
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

            if (result.Action == "set")
            {
                ExecuteSet(doc, request, result);
            }
            else
            {
                ExecuteGet(doc, request, result);
            }

            return result;
        }

        private void ExecuteGet(Document doc, ManageParametersRequest request, ManageParametersResult result)
        {
            foreach (long elemId in request.ElementIds)
            {
                var elemResult = new ElementParametersResult { ElementId = elemId };
                var elem = doc.GetElement(ElementIdExtensions.FromLongId(elemId));

                if (elem == null)
                {
                    elemResult.Success = false;
                    elemResult.ErrorMessage = $"Element with ID {elemId} not found.";
                    result.Elements.Add(elemResult);
                    continue;
                }

                // If specific parameters requested
                if (request.Parameters != null && request.Parameters.Count > 0)
                {
                    foreach (var spec in request.Parameters)
                    {
                        var param = FindParameter(elem, spec);
                        string key = !string.IsNullOrWhiteSpace(spec.Name) ? spec.Name! :
                                     (!string.IsNullOrWhiteSpace(spec.BuiltInName) ? spec.BuiltInName! : spec.Guid ?? "Unknown");

                        if (param == null)
                        {
                            elemResult.Parameters[key] = new ParameterValueResult
                            {
                                Name = key,
                                ErrorMessage = "Parameter not found on element."
                            };
                            continue;
                        }

                        elemResult.Parameters[param.Definition?.Name ?? key] = ExtractParamValue(param, spec.Unit);
                    }
                }
                else
                {
                    // Return all non-empty parameters
                    foreach (Parameter p in elem.Parameters)
                    {
                        if (p.HasValue && !string.IsNullOrWhiteSpace(p.Definition?.Name))
                        {
                            string pName = p.Definition!.Name;
                            if (!elemResult.Parameters.ContainsKey(pName))
                            {
                                elemResult.Parameters[pName] = ExtractParamValue(p, null);
                            }
                        }
                    }
                }

                result.Elements.Add(elemResult);
            }

            result.Success = true;
        }

        private void ExecuteSet(Document doc, ManageParametersRequest request, ManageParametersResult result)
        {
            if (request.Parameters == null || request.Parameters.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Parameters list cannot be empty for 'set' action.";
                return;
            }

            var warningContext = new WarningCollectorContext();
            int updatedCount = 0;

            string txName = HumanFriendlyNaming.ResolveTransactionName(request.TransactionName, request.Action, "parameters");
            using (var trans = new Transaction(doc, txName))
            {
                var failureOptions = trans.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new SmartFailuresPreprocessor(warningContext));
                failureOptions.SetClearAfterRollback(true);
                trans.SetFailureHandlingOptions(failureOptions);

                trans.Start();

                foreach (long elemId in request.ElementIds)
                {
                    var elemResult = new ElementParametersResult { ElementId = elemId };
                    var elem = doc.GetElement(ElementIdExtensions.FromLongId(elemId));

                    if (elem == null)
                    {
                        elemResult.Success = false;
                        elemResult.ErrorMessage = $"Element with ID {elemId} not found.";
                        result.Elements.Add(elemResult);
                        continue;
                    }

                    foreach (var spec in request.Parameters)
                    {
                        string key = !string.IsNullOrWhiteSpace(spec.Name) ? spec.Name! :
                                     (!string.IsNullOrWhiteSpace(spec.BuiltInName) ? spec.BuiltInName! : spec.Guid ?? "Unknown");

                        var param = FindParameter(elem, spec);
                        if (param == null)
                        {
                            elemResult.Parameters[key] = new ParameterValueResult
                            {
                                Name = key,
                                Updated = false,
                                ErrorMessage = "Parameter not found on element."
                            };
                            continue;
                        }

                        string actualName = param.Definition?.Name ?? key;

                        if (param.IsReadOnly)
                        {
                            elemResult.Parameters[actualName] = new ParameterValueResult
                            {
                                Name = actualName,
                                Updated = false,
                                IsReadOnly = true,
                                ErrorMessage = "Parameter is read-only."
                            };
                            continue;
                        }

                        try
                        {
                            SetParameterValue(param, spec.Value, spec.Unit);
                            updatedCount++;

                            var updatedValue = ExtractParamValue(param, spec.Unit);
                            updatedValue.Updated = true;
                            elemResult.Parameters[actualName] = updatedValue;
                        }
                        catch (Exception ex)
                        {
                            elemResult.Parameters[actualName] = new ParameterValueResult
                            {
                                Name = actualName,
                                Updated = false,
                                ErrorMessage = $"Failed to set value: {ex.Message}"
                            };
                        }
                    }

                    result.Elements.Add(elemResult);
                }

                trans.Commit();
            }

            result.TotalUpdated = updatedCount;
            result.Success = true;
            var warnings = warningContext.GetWarnings();
            if (warnings.Count > 0)
            {
                result.Warnings = new List<WarningRecord>(warnings);
            }
        }

        private static Parameter? FindParameter(Element elem, ParameterSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.Guid) && Guid.TryParse(spec.Guid, out var guid))
            {
                var p = elem.get_Parameter(guid);
                if (p != null) return p;
            }

            if (!string.IsNullOrWhiteSpace(spec.BuiltInName) &&
                Enum.TryParse<BuiltInParameter>(spec.BuiltInName, true, out var bip))
            {
                var p = elem.get_Parameter(bip);
                if (p != null) return p;
            }

            if (!string.IsNullOrWhiteSpace(spec.Name))
            {
                var p = elem.LookupParameter(spec.Name);
                if (p != null) return p;

                // Case-insensitive fallback
                foreach (Parameter existing in elem.Parameters)
                {
                    if (string.Equals(existing.Definition?.Name, spec.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return existing;
                    }
                }
            }

            return null;
        }

        private static ParameterValueResult ExtractParamValue(Parameter param, string? requestedUnit)
        {
            var res = new ParameterValueResult
            {
                Name = param.Definition?.Name ?? "Unnamed",
                StorageType = param.StorageType.ToString(),
                IsReadOnly = param.IsReadOnly,
                DisplayValue = param.AsValueString()
            };

            switch (param.StorageType)
            {
                case StorageType.String:
                    res.Value = param.AsString();
                    if (res.DisplayValue == null) res.DisplayValue = res.Value?.ToString();
                    break;

                case StorageType.Integer:
                    res.Value = param.AsInteger();
                    if (res.DisplayValue == null) res.DisplayValue = res.Value?.ToString();
                    break;

                case StorageType.Double:
                    double internalVal = param.AsDouble();
                    if (!string.IsNullOrWhiteSpace(requestedUnit))
                    {
                        res.Value = RevitUnitConverter.ConvertFromInternal(internalVal, requestedUnit);
                        res.Unit = requestedUnit;
                    }
                    else
                    {
                        res.Value = internalVal;
                    }
                    break;

                case StorageType.ElementId:
                    var eid = param.AsElementId();
                    res.Value = eid != null ? eid.ToLongId() : -1;
                    if (res.DisplayValue == null) res.DisplayValue = res.Value?.ToString();
                    break;

                default:
                    res.Value = null;
                    break;
            }

            return res;
        }

        private static void SetParameterValue(Parameter param, object? rawValue, string? unit)
        {
            if (rawValue == null)
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(string.Empty);
                        break;
                    case StorageType.Integer:
                        param.Set(0);
                        break;
                    case StorageType.Double:
                        param.Set(0.0);
                        break;
                    case StorageType.ElementId:
                        param.Set(ElementId.InvalidElementId);
                        break;
                }
                return;
            }

            switch (param.StorageType)
            {
                case StorageType.String:
                    param.Set(rawValue.ToString());
                    break;

                case StorageType.Integer:
                    if (rawValue is bool b)
                    {
                        param.Set(b ? 1 : 0);
                    }
                    else
                    {
                        int intVal = Convert.ToInt32(rawValue);
                        param.Set(intVal);
                    }
                    break;

                case StorageType.Double:
                    double numVal = Convert.ToDouble(rawValue);
                    if (!string.IsNullOrWhiteSpace(unit))
                    {
                        numVal = RevitUnitConverter.ConvertToInternal(numVal, unit);
                    }
                    param.Set(numVal);
                    break;

                case StorageType.ElementId:
                    long idVal = Convert.ToInt64(rawValue);
                    param.Set(ElementIdExtensions.FromLongId(idVal));
                    break;
            }
        }
    }
}
