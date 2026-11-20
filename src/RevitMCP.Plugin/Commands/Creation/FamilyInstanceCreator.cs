using System;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;

namespace RevitMCP.Plugin.Commands.Creation
{
    public static class FamilyInstanceCreator
    {
        public static (long id, string summary) CreateFamilyInstance(Document doc, CreateFamilyInstanceSpec spec)
        {
            FamilySymbol? symbol = null;
            if (spec.FamilySymbolId.HasValue && spec.FamilySymbolId.Value > 0)
            {
                symbol = doc.GetElement(ElementIdExtensions.FromLongId(spec.FamilySymbolId.Value)) as FamilySymbol;
            }
            else if (!string.IsNullOrEmpty(spec.FamilyName) || !string.IsNullOrEmpty(spec.TypeName))
            {
                var query = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>();

                if (!string.IsNullOrEmpty(spec.FamilyName))
                {
                    query = query.Where(s => string.Equals(s.FamilyName, spec.FamilyName, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(spec.TypeName))
                {
                    query = query.Where(s => string.Equals(s.Name, spec.TypeName, StringComparison.OrdinalIgnoreCase));
                }

                symbol = query.FirstOrDefault();
            }

            if (symbol == null)
            {
                throw new InvalidOperationException("Could not find matching FamilySymbol in project.");
            }

            var instance = FamilyInstanceDispatcher.CreateInstance(doc, symbol, spec);

            var idVal = instance.Id.ToLongId();
            return (idVal, $"FamilyInstance '{symbol.FamilyName}: {symbol.Name}' created (ID: {idVal}).");
        }
    }
}
