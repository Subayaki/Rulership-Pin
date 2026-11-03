using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
namespace RulershipPin;
internal static class LegacyGreeting
{
 // Read a legacy literal as data, without loading or executing the old assembly.
 internal static string? Read(string assembly) {
  try {
   using var input=File.OpenRead(assembly);using var pe=new PEReader(input);var metadata=pe.GetMetadataReader();
   foreach(var handle in metadata.TypeDefinitions) {
    var type=metadata.GetTypeDefinition(handle);
    if(metadata.GetString(type.Namespace)!="RulershipPin" || metadata.GetString(type.Name)!="ChibiCompanion") continue;
    foreach(var methodHandle in type.GetMethods()) {
     var method=metadata.GetMethodDefinition(methodHandle);
     if(metadata.GetString(method.Name)!="LetterSign" || method.RelativeVirtualAddress==0) continue;
     byte[] il=pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()??[];
     for(int i=0;i+4<il.Length;i++) if(il[i]==0x72 && il[i+4]==0x70) {
      int token=BitConverter.ToInt32(il,i+1)&0x00ffffff;
      string text=metadata.GetUserString(MetadataTokens.UserStringHandle(token));
      if(text.StartsWith("Hello\n",StringComparison.Ordinal) && text.EndsWith("<3",StringComparison.Ordinal) && text.Length is >8 and <=40) return text.Replace('\n',' ');
     }
    }
   }
  } catch(Exception error) when(error is IOException or BadImageFormatException or ArgumentException) { }
  return null;
 }
 internal static bool Import(string target) {
  string path=Path.Combine(target,"companion-settings.json");
  if(!File.Exists(path)) return false;
  if(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("The settings file must not be a link.");
  var settings=JsonNode.Parse(File.ReadAllText(path)) as JsonObject??throw new InvalidDataException("The settings file is invalid.");
  if(settings["Greeting"] is not null) return false;
  string? greeting=Read(Path.Combine(target,"Rulership-Pin.dll"));if(greeting is null) return false;
  settings["Greeting"]=greeting;
  File.WriteAllText(path+".tmp",settings.ToJsonString(new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));File.Move(path+".tmp",path,true);return true;
 }
}
