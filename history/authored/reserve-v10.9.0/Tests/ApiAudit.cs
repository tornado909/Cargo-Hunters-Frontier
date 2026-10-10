using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Mono.Cecil;
static class ApiAudit
{
 static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> ts){foreach(var t in ts){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}}
 static bool ArrayIntrinsic(MethodReference m)
 {
  if(m.DeclaringType is not ArrayType a||a.Rank<1)return false;
  int n=m.Parameters.Count;bool Ints(int count)=>m.Parameters.Take(count).All(p=>p.ParameterType.FullName=="System.Int32");
  return m.Name switch{
   ".ctor"=>m.ReturnType.FullName=="System.Void"&&(n==a.Rank||n==a.Rank*2)&&Ints(n),
   "Get"=>n==a.Rank&&Ints(n)&&m.ReturnType.FullName==a.ElementType.FullName,
   "Set"=>n==a.Rank+1&&Ints(a.Rank)&&m.Parameters.Last().ParameterType.FullName==a.ElementType.FullName&&m.ReturnType.FullName=="System.Void",
   "Address"=>n==a.Rank&&Ints(n)&&m.ReturnType is ByReferenceType br&&br.ElementType.FullName==a.ElementType.FullName,
   _=>false};
 }
 static int Main(string[] args)
 {
  string root=args[0];var resolver=new DefaultAssemblyResolver();
  foreach(var d in args.Skip(1))resolver.AddSearchDirectory(d);resolver.AddSearchDirectory(root);
  var runtimeIntrinsics=new List<string>();var failures=new List<string>();var signatures=new SortedSet<string>();
  foreach(string file in new[]{"Frontier.Reserve.dll"})
  {
   var a=AssemblyDefinition.ReadAssembly(Path.Combine(root,file),new ReaderParameters{AssemblyResolver=resolver});
   foreach(var member in a.MainModule.GetMemberReferences())
   {
    if(member is MethodReference intrinsic && ArrayIntrinsic(intrinsic)){runtimeIntrinsics.Add(intrinsic.FullName);continue;}
    // Framework/interop/game member resolution, without running static constructors or IL2CPP.
    try {if(member is MethodReference m&&m.Resolve()==null)failures.Add(file+": "+member);else if(member is FieldReference f&&f.Resolve()==null)failures.Add(file+": "+member);else signatures.Add(member.FullName);}
    catch(Exception e){failures.Add(file+": "+member+" -> "+e.Message);}
   }
   foreach(var t in All(a.MainModule.Types))foreach(var m in t.Methods.Where(m=>m.HasBody))
    foreach(var i in m.Body.Instructions.Where(i=>i.Operand is Mono.Cecil.Cil.Instruction))
     if(!m.Body.Instructions.Contains((Mono.Cecil.Cil.Instruction)i.Operand))failures.Add("Invalid branch: "+m.FullName);
  }
  var report=new{scope="Compile-time member references and method branch targets; not a Unity/IL2CPP runtime test",resolvedMembers=signatures.Count,runtimeIntrinsics,failures,signatures};
  File.WriteAllText(Path.Combine(root,"API-AUDIT.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"Resolved members: {signatures.Count}; failures: {failures.Count}");foreach(var f in failures)Console.WriteLine(f);return failures.Count>0?1:0;
 }
}
