using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RulershipPin;
internal static class UpdateDiagnostics
{
 internal static void RunLegacy(string assembly,string directory) {
  string target=Path.Combine(directory,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(target);
  File.Copy(assembly,Path.Combine(target,"Rulership-Pin.dll"));File.WriteAllText(Path.Combine(target,"companion-settings.json"),"{\"CompanionScale\":0.65}");
  string expected=LegacyGreeting.Read(assembly)??throw new InvalidDataException("Legacy greeting was not detected");
  if(!LegacyGreeting.Import(target)) throw new InvalidDataException("Legacy greeting was not imported");
  using var settings=JsonDocument.Parse(File.ReadAllText(Path.Combine(target,"companion-settings.json")));
  if(settings.RootElement.GetProperty("Greeting").GetString()!=expected || settings.RootElement.GetProperty("CompanionScale").GetDouble()!=.65 || LegacyGreeting.Import(target)) throw new InvalidDataException("Legacy migration overwrote existing preferences");
  File.WriteAllText(Path.Combine(directory,"legacy-greeting-checks.txt"),"PASS: previous hardcoded greeting detected without loading old code; imported into local settings; size preserved; second import does not overwrite an existing greeting. No personal text included in this report.\n");
 }
 internal static void Run(string directory) {
  string root=Path.Combine(directory,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var files=new Dictionary<string,string> { ["Rulership-Pin.exe"]="new exe",["Rulership-Pin.dll"]="new dll",["Assets/new.png"]="new asset"};
  var manifest=new PackageManifest("1.1.0",files.ToDictionary(p=>p.Key,p=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Value)))));
  string zip=Path.Combine(root,"valid.zip");MakeZip(zip,files,manifest);
  string stage=Path.Combine(root,"stage");var verified=UpdatePackage.Extract(zip,stage,UpdatePackage.Hash(zip),"1.1.0");
  const string personalSettings="{\"Greeting\":\"Hello friend <3\",\"UserName\":\"Sky\",\"CompanionScale\":1}";
  string target=Path.Combine(root,"installed");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"Rulership-Pin.exe"),"old exe");File.WriteAllText(Path.Combine(target,"Rulership-Pin.dll"),"old dll");File.WriteAllText(Path.Combine(target,"companion-settings.json"),personalSettings);
  bool rolledBack=false;
  try {UpdatePackage.Install(stage,target,verified,n=>{if(n==3) throw new IOException("simulated file-copy failure");});}catch(IOException) {rolledBack=true;}
  if(!rolledBack || File.ReadAllText(Path.Combine(target,"Rulership-Pin.exe"))!="old exe" || File.Exists(Path.Combine(target,"Assets/new.png"))) throw new InvalidDataException("Rollback failed");
  string backup=UpdatePackage.Install(stage,target,verified);
  if(File.ReadAllText(Path.Combine(target,"companion-settings.json"))!=personalSettings || File.ReadAllText(Path.Combine(target,"Rulership-Pin.exe"))!="new exe" || File.ReadAllText(Path.Combine(backup,"Rulership-Pin.exe"))!="old exe") throw new InvalidDataException("Install/backup/settings preservation failed");
  Reject(()=>UpdatePackage.Extract(zip,Path.Combine(root,"wrong-hash"),new string('0',64),"1.1.0"));
  Reject(()=>UpdatePackage.Extract(zip,Path.Combine(root,"wrong-version"),UpdatePackage.Hash(zip),"9.0.0"));
  foreach(string path in new[]{"../outside.exe","Assets/../../outside.exe","/absolute.exe","C:/evil.exe","Assets\\evil.exe","companion-settings.json","update-history/evil.txt","Assets/CON.txt","Assets/LPT1","Assets/bad?.png"}) Reject(()=>UpdatePackage.SafePath(target,path));
  foreach(string unsafeEntry in new[]{"Rulership_pin_custom/../outside.exe","other/Rulership-Pin.exe","Rulership_pin_custom/companion-settings.json","Rulership_pin_custom/RULERSHIP-PIN.EXE"}) {
   string bad=Path.Combine(root,Guid.NewGuid()+".zip");MakeZip(bad,files,manifest,unsafeEntry);
   Reject(()=>UpdatePackage.Extract(bad,Path.Combine(root,Guid.NewGuid().ToString("N")),UpdatePackage.Hash(bad),"1.1.0"));
  }
  AppUpdater.ValidateAssetUrl("https://github.com/Subayaki/Rulership-Pin/releases/download/v1.1.0/"+AppUpdater.AssetName,"v1.1.0",AppUpdater.AssetName);
  Reject(()=>AppUpdater.ValidateAssetUrl("https://example.com/update.zip","v1.1.0",AppUpdater.AssetName));
  Reject(()=>AppUpdater.ValidateAssetUrl("https://github.com/other/repo/releases/download/v1.1.0/"+AppUpdater.AssetName,"v1.1.0",AppUpdater.AssetName));
  File.WriteAllText(Path.Combine(directory,"updater-checks.txt"),"PASS: package hash/version/file verification; traversal and duplicate/root/settings/backup path rejection; release URL pinning; transactional install; injected copy failure rolls back changed/new files; original settings preserved; previous version backup retained. No installed app or desktop instance was changed.\n");
 }
 static void Reject(Action action) {bool rejected=false;try {action();}catch(InvalidDataException) {rejected=true;}if(!rejected) throw new InvalidDataException("An invalid update was accepted");}
 static void MakeZip(string path,Dictionary<string,string> files,PackageManifest manifest,string? extra=null) {
  using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
  foreach(var file in files) {using var writer=new StreamWriter(zip.CreateEntry(UpdatePackage.Root+file.Key).Open());writer.Write(file.Value);}
  using(var writer=new StreamWriter(zip.CreateEntry(UpdatePackage.Root+UpdatePackage.ManifestName).Open())) writer.Write(JsonSerializer.Serialize(manifest));
  if(extra is not null) {using var writer=new StreamWriter(zip.CreateEntry(extra).Open());writer.Write("invalid");}
 }
}
