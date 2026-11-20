using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCP.Plugin.Utils
{
    public static class ElementIdExtensions
    {
        public static long ToLongId(this ElementId id)
        {
            if (id == null) return -1;
#if REVIT_2019 || REVIT_2020 || REVIT_2021 || REVIT_2022 || REVIT_2023
            return (long)id.IntegerValue;
#else
            return id.Value;
#endif
        }

        public static ElementId FromLongId(long id)
        {
#if REVIT_2019 || REVIT_2020 || REVIT_2021 || REVIT_2022 || REVIT_2023
            return new ElementId((int)id);
#else
            return new ElementId(id);
#endif
        }
    }
}
