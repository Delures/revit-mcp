using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;

namespace RevitMCP.Plugin.Utils
{
    /// <summary>
    /// Multi-version Floor creation utility compatible with Revit 2019 through 2027+.
    /// Handles the API transition between doc.Create.NewFloor (legacy) and Floor.Create (modern).
    /// </summary>
    public static class FloorCreator
    {
        public static Floor CreateFloor(Document doc, IList<CurveLoop> profileLoops, ElementId floorTypeId, ElementId levelId, bool isStructural)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (profileLoops == null || profileLoops.Count == 0) throw new ArgumentException("Profile loops cannot be empty.", nameof(profileLoops));

            Floor? floor = null;

            // 1. Try modern Floor.Create method (Revit 2022 - 2027+)
            MethodInfo? createMethod = typeof(Floor).GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Document), typeof(IList<CurveLoop>), typeof(ElementId), typeof(ElementId) },
                null);

            if (createMethod != null)
            {
                floor = createMethod.Invoke(null, new object[] { doc, profileLoops, floorTypeId, levelId }) as Floor;
            }
            else
            {
                // 2. Fallback for Revit 2019 - 2021: doc.Create.NewFloor
                var floorType = doc.GetElement(floorTypeId) as FloorType;
                var level = doc.GetElement(levelId) as Level;

                var curveArray = new CurveArray();
                foreach (Curve c in profileLoops[0])
                {
                    curveArray.Append(c);
                }

                MethodInfo? newFloorMethod = doc.Create.GetType().GetMethod(
                    "NewFloor",
                    new[] { typeof(CurveArray), typeof(FloorType), typeof(Level), typeof(bool) });

                if (newFloorMethod != null)
                {
                    floor = newFloorMethod.Invoke(doc.Create, new object[] { curveArray, floorType!, level!, isStructural }) as Floor;
                }
                else
                {
                    MethodInfo? newFloorWithNormal = doc.Create.GetType().GetMethod(
                        "NewFloor",
                        new[] { typeof(CurveArray), typeof(FloorType), typeof(Level), typeof(bool), typeof(XYZ) });

                    if (newFloorWithNormal != null)
                    {
                        floor = newFloorWithNormal.Invoke(doc.Create, new object[] { curveArray, floorType!, level!, isStructural, XYZ.BasisZ }) as Floor;
                    }
                }
            }

            if (floor == null)
            {
                throw new InvalidOperationException("Failed to create Floor: no compatible creation method found on this Revit version.");
            }

            // Ensure structural parameter
            var structuralParam = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
            if (structuralParam != null && !structuralParam.IsReadOnly)
            {
                structuralParam.Set(isStructural ? 1 : 0);
            }

            return floor;
        }
    }
}
