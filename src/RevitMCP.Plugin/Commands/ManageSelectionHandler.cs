using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;

namespace RevitMCP.Plugin.Commands
{
    public class ManageSelectionHandler
    {
        public ManageSelectionResult Execute(UIApplication app, ManageSelectionRequest request)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var uidoc = app.ActiveUIDocument;
            if (uidoc == null)
            {
                return new ManageSelectionResult
                {
                    Success = false,
                    ErrorMessage = "No active document found in Revit."
                };
            }

            var doc = uidoc.Document;
            string action = request.Action?.Trim().ToLowerInvariant() ?? "get";

            if (action == "set")
            {
                var idsToSelect = new List<ElementId>();
                if (request.ElementIds != null)
                {
                    foreach (var id in request.ElementIds)
                    {
                        var elem = doc.GetElement(ElementIdExtensions.FromLongId(id));
                        if (elem != null)
                        {
                            idsToSelect.Add(elem.Id);
                        }
                    }
                }

                uidoc.Selection.SetElementIds(idsToSelect);

                return new ManageSelectionResult
                {
                    Success = true,
                    Action = "set",
                    SelectedElementIds = idsToSelect.Select(x => (int)x.ToLongId()).ToList()
                };
            }
            else
            {
                var currentSelection = uidoc.Selection.GetElementIds();
                var resultIds = new List<int>();
                var elementInfos = new List<SelectedElementInfo>();

                foreach (var id in currentSelection)
                {
                    var elem = doc.GetElement(id);
                    if (elem == null) continue;

                    int intId = (int)elem.Id.ToLongId();
                    resultIds.Add(intId);

                    string? familyName = null;
                    string? typeName = null;

                    var typeId = elem.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        if (doc.GetElement(typeId) is ElementType et)
                        {
                            familyName = et.FamilyName;
                            typeName = et.Name;
                        }
                    }

                    elementInfos.Add(new SelectedElementInfo
                    {
                        Id = intId,
                        Category = elem.Category?.Name,
                        FamilyName = familyName,
                        TypeName = typeName,
                        Name = elem.Name
                    });
                }

                return new ManageSelectionResult
                {
                    Success = true,
                    Action = "get",
                    SelectedElementIds = resultIds,
                    Elements = elementInfos
                };
            }
        }
    }
}
