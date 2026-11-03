using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace RulershipPin;

internal sealed record PackageManifest(string Version,Dictionary<string,string> Files);
internal static class UpdatePackage
{
 internal const string Root="Rulership_pin_custom/",ManifestName="update-manifest.json";
 internal static string Hash(string file) {using var stream=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(stream));}
 internal static string SafePath(string root,string relative,bool allowBackup=false) {
  if(string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.Split('/').Any(p=>p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' '))) throw new InvalidDataException("Unsafe update path.");
  foreach(string component in relative.Split('/')) {
   string stem=component.Split('.')[0].ToUpperInvariant();
   if(component.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length==4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9')) throw new InvalidDataException("Invalid Windows update filename.");
  }
  if(Path.GetFileName(relative).StartsWith("companion-settings.json",StringComparison.OrdinalIgnoreCase) || (!allowBackup && relative.StartsWith("update-history",StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Update contains personal settings or backup files.");
  string full=Path.GetFullPath(Path.Combine(root,relative)),prefix=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+Path.DirectorySeparatorChar;
  if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update path escapes its folder.");
  for(string? check=full;check is not null;check=Path.GetDirectoryName(check)) if((File.Exists(check)||Directory.Exists(check)) && File.GetAttributes(check).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Update folders must not contain links.");
  return full;
 }
 internal static PackageManifest Extract(string archive,string destination,string expectedHash,string expectedVersion) {
  if(!Hash(archive).Equals(expectedHash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update checksum does not match. Nothing was installed.");
  if(Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) throw new InvalidDataException("Update staging folder is not empty.");
  Directory.CreateDirectory(destination);
  using var zip=ZipFile.OpenRead(archive);
  if(zip.Entries.Count>3000 || zip.Entries.Sum(e=>e.Length)>1_500_000_000) throw new InvalidDataException("Update package is too large.");
  var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in zip.Entries) {
   if(!entry.FullName.StartsWith(Root,StringComparison.Ordinal)) throw new InvalidDataException("Unexpected update folder.");
   if(entry.FullName.EndsWith('/')) continue;
   string relative=entry.FullName[Root.Length..];SafePath(destination,relative);
   if(((entry.ExternalAttributes>>16)&0xF000)==0xA000 || !entries.TryAdd(relative,entry)) throw new InvalidDataException("Duplicate path or link in update.");
  }
  if(!entries.TryGetValue(ManifestName,out var manifestEntry) || manifestEntry.Length>2_000_000) throw new InvalidDataException("Update manifest is missing or too large.");
  PackageManifest manifest;
  using(var input=manifestEntry.Open()) manifest=JsonSerializer.Deserialize<PackageManifest>(input)??throw new InvalidDataException("Invalid update manifest.");
  if(manifest.Version!=expectedVersion || manifest.Files is null || manifest.Files.Count+1!=entries.Count || !manifest.Files.ContainsKey("Rulership-Pin.exe") || !manifest.Files.ContainsKey("Rulership-Pin.dll")) throw new InvalidDataException("Update version or file list is invalid.");
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var file in manifest.Files) {
   if(!seen.Add(file.Key) || file.Key.Equals(ManifestName,StringComparison.OrdinalIgnoreCase) || file.Value.Length!=64 || !file.Value.All(Uri.IsHexDigit) || !entries.TryGetValue(file.Key,out var entry)) throw new InvalidDataException("Invalid update file list.");
   string path=SafePath(destination,file.Key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   using(var input=entry.Open()) using(var output=new FileStream(path,FileMode.CreateNew)) input.CopyTo(output);
   if(!Hash(path).Equals(file.Value,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("An update file failed its checksum check.");
  }
  File.WriteAllText(Path.Combine(destination,ManifestName),JsonSerializer.Serialize(manifest));return manifest;
 }
 internal static string Install(string payload,string target,PackageManifest manifest,Action<int>? afterCopy=null) {
  target=Path.GetFullPath(target);
  if(Path.TrimEndingDirectorySeparator(target)==Path.TrimEndingDirectorySeparator(Path.GetPathRoot(target)!) || !File.Exists(Path.Combine(target,"Rulership-Pin.exe"))) throw new InvalidDataException("The installed app folder could not be verified.");
  // Validate every destination before touching the installation.
  foreach(var file in manifest.Files) {SafePath(target,file.Key);if(!Hash(SafePath(payload,file.Key)).Equals(file.Value,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Staged update changed after verification.");}
  LegacyGreeting.Import(target);
  string backup=SafePath(target,"update-history/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"),true);
  Directory.CreateDirectory(backup);
  var copied=new List<(string path,string? backup)>();
  try {
   foreach(string relative in manifest.Files.Keys.Append(ManifestName)) {
    string destination=SafePath(target,relative),source=SafePath(payload,relative);string? saved=null;
    if(File.Exists(destination)) {saved=SafePath(backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(saved)!);File.Copy(destination,saved);}
    copied.Add((destination,saved));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(source,destination,true);afterCopy?.Invoke(copied.Count);
   }
   return backup;
  } catch(Exception installError) {
   var restoreErrors=new List<Exception>();
   foreach(var file in copied.AsEnumerable().Reverse()) try {if(file.backup is null) File.Delete(file.path);else File.Copy(file.backup,file.path,true);} catch(Exception restoreError) {restoreErrors.Add(restoreError);}
   if(restoreErrors.Count>0) throw new AggregateException("Update failed; some files need manual restoration from "+backup,new[]{installError}.Concat(restoreErrors));
   throw new IOException("Update failed. The previous files were restored. Backup: "+backup,installError);
  }
 }
}
