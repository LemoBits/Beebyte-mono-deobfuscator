using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

public static class CecilExtensions
{
    public static IEnumerable<TypeDefinition> GetAllTypes(this ModuleDefinition module)
    {
        return module.Types.SelectMany(GetSelfAndNested);
    }

    private static IEnumerable<TypeDefinition> GetSelfAndNested(TypeDefinition type)
    {
        var types = new List<TypeDefinition> { type };
        if (type.HasNestedTypes)
            types.AddRange(type.NestedTypes.SelectMany(GetSelfAndNested));
        return types;
    }
}