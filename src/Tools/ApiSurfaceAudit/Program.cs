using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ApiSurfaceAudit ORIGINAL.dll CANDIDATE.dll");
    return 2;
}

static bool ExternallyVisible(TypeAttributes a)
{
    var visibility = a & TypeAttributes.VisibilityMask;
    return visibility is TypeAttributes.Public or TypeAttributes.NestedPublic
        or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem;
}

static bool ExternallyVisibleMethod(MethodAttributes a)
{
    var access = a & MethodAttributes.MemberAccessMask;
    return access is MethodAttributes.Public or MethodAttributes.Family
        or MethodAttributes.FamORAssem;
}

static bool ExternallyVisibleField(FieldAttributes a)
{
    var access = a & FieldAttributes.FieldAccessMask;
    return access is FieldAttributes.Public or FieldAttributes.Family
        or FieldAttributes.FamORAssem;
}

static string TypeName(MetadataReader md, TypeDefinitionHandle handle)
{
    var type = md.GetTypeDefinition(handle);
    var name = md.GetString(type.Name);
    if (!type.GetDeclaringType().IsNil)
        return TypeName(md, type.GetDeclaringType()) + "+" + name;
    var ns = md.GetString(type.Namespace);
    return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
}

static (string Assembly, string Version, HashSet<string> Types, HashSet<string> Api) ReadSurface(string file)
{
    using var stream = File.OpenRead(file);
    using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
    if (!pe.HasMetadata)
        throw new InvalidDataException("No managed assembly metadata: " + file);
    var md = pe.GetMetadataReader();
    var assembly = md.GetAssemblyDefinition();
    var identity = md.GetString(assembly.Name);
    var version = assembly.Version.ToString();

    HashSet<string> types = new(StringComparer.Ordinal);
    HashSet<string> exported = new(StringComparer.Ordinal);
    foreach (var handle in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(handle);
        var fullname = TypeName(md, handle);
        var unqualified = md.GetString(type.Name);
        if (unqualified.StartsWith('<')) continue;  // compiler-generated / anonymous
        types.Add(fullname);
        if (!ExternallyVisible(type.Attributes))
            continue;
        exported.Add("TYPE " + fullname);
        foreach (var methodHandle in type.GetMethods())
        {
            var method = md.GetMethodDefinition(methodHandle);
            var name = md.GetString(method.Name);
            if (!ExternallyVisibleMethod(method.Attributes) || name.StartsWith('<'))
                continue;
            int argc = method.GetParameters().Count(h => md.GetParameter(h).SequenceNumber != 0);
            int generics = method.GetGenericParameters().Count;
            exported.Add($"METHOD {fullname}.{name}/{argc}/{generics}");
        }
        foreach (var fieldHandle in type.GetFields())
        {
            var field = md.GetFieldDefinition(fieldHandle);
            if (!ExternallyVisibleField(field.Attributes))
                continue;
            var name = md.GetString(field.Name);
            if (!name.StartsWith('<'))
                exported.Add("FIELD " + fullname + "." + name);
        }
    }
    return (identity, version, types, exported);
}

try
{
    var original = ReadSurface(args[0]);
    var rebuilt = ReadSurface(args[1]);
    var missing = original.Api.Except(rebuilt.Api).OrderBy(x => x).ToArray();
    var added = rebuilt.Api.Except(original.Api).OrderBy(x => x).ToArray();
    var absentTypes = original.Types.Except(rebuilt.Types).OrderBy(x => x).ToArray();
    var extraTypes = rebuilt.Types.Except(original.Types).OrderBy(x => x).ToArray();
    var result = new
    {
        original_assembly = original.Assembly,
        rebuilt_assembly = rebuilt.Assembly,
        original_version = original.Version,
        rebuilt_version = rebuilt.Version,
        original_declared_types = original.Types.Count,
        rebuilt_declared_types = rebuilt.Types.Count,
        original_visible_members = original.Api.Count,
        rebuilt_visible_members = rebuilt.Api.Count,
        missing_visible_members = missing,
        new_visible_members = added,
        missing_named_types = absentTypes,
        new_named_types = extraTypes,
        public_surface_ok = original.Assembly == rebuilt.Assembly && original.Version == rebuilt.Version && missing.Length == 0 && added.Length == 0 && absentTypes.Length == 0
    };
    Console.WriteLine(JsonSerializer.Serialize(result));
    return result.public_surface_ok ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    return 2;
}
