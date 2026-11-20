using System;
using Autodesk.Revit.DB;

namespace RevitMCP.Plugin.Utils
{
    public static class CategoryResolver
    {
        public static ElementId? ResolveCategoryId(Document doc, string catName)
        {
            if (string.IsNullOrWhiteSpace(catName)) return null;

            if (Enum.TryParse<BuiltInCategory>(catName, true, out var bic) ||
                Enum.TryParse<BuiltInCategory>("OST_" + catName, true, out bic))
            {
                var cat = doc.Settings.Categories.get_Item(bic);
                if (cat != null) return cat.Id;
            }

            foreach (Category c in doc.Settings.Categories)
            {
                if (string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase))
                {
                    return c.Id;
                }
            }

            return null;
        }
    }
}
