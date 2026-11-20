using System;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Plugin.Scripting
{
    public interface IRevitScriptContext : IScriptExecutionContext
    {
        UIApplication App { get; }
        Application Application { get; }
        UIDocument? UIDoc { get; }
        Document? Doc { get; }

        T WithTransaction<T>(string transactionName, Func<T> action);
        void WithTransaction(string transactionName, Action action);

        T WithTransactionGroup<T>(string groupName, Func<T> action);
        void WithTransactionGroup(string groupName, Action action);

        void AddAffectedElements(IEnumerable<ElementId> elementIds);
    }
}
