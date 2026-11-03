using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
namespace RulershipPin;

internal sealed record UpdateJob(string Archive,string Hash,string Version,string Target,int ParentId,long ParentStartTicks);
internal sealed record ReleaseAsset([property:JsonPropertyName("name")]string Name,[property:JsonPropertyName("browser_download_url")]string Url,[property:JsonPropertyName("digest")]string? Digest);
internal sealed record ReleaseInfo([property:JsonPropertyName("tag_name")]string Tag,[property:JsonPropertyName("draft")]bool Draft,[property:JsonPropertyName("prerelease")]bool Prerelease,[property:JsonPropertyName("assets")]ReleaseAsset[] Assets);
internal static class AppUpdater
{
 internal const string Repository="Subayaki/Rulership-Pin",AssetName="Rulership-Pin-Portable-win-x64.zip";
 internal static Version Current=>Assembly.GetExecutingAssembly().GetName().Version??new Version(1,0,0);
 static HttpClient Client() {var client=new HttpClient {Timeout=TimeSpan.FromMinutes(5)};client.DefaultRequestHeaders.UserAgent.ParseAdd("Rulership-Pin/"+Current);client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");return client;}
 internal static void ValidateAssetUrl(string url,string tag,string name) {
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="github.com" || uri.AbsolutePath!=$"/{Repository}/releases/download/{tag}/{name}" || uri.Query.Length>0 || uri.Fragment.Length>0) throw new InvalidDataException("The update download does not belong to this app's release repository.");
 }
 internal static async Task<ReleaseInfo?> Latest(HttpClient client) {
  using var response=await client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
  if(response.StatusCode==HttpStatusCode.NotFound) return null;response.EnsureSuccessStatusCode();
  using var stream=await response.Content.ReadAsStreamAsync();return await JsonSerializer.DeserializeAsync<ReleaseInfo>(stream);
 }
 internal static async Task Check(Window owner,MenuItem item) {
  item.IsEnabled=false;item.Header="Checking for updates…";
  using var cancellation=new CancellationTokenSource();Window? progress=null;TextBlock? progressText=null;
  void Report(string text) {item.Header=text;if(progressText is not null) progressText.Text=text;}
  try {
   using var client=Client();var release=await Latest(client);
   if(release is null) {MessageBox.Show(owner,"No published update is available yet.","Rulership-Pin – Updates");return;}
   if(release.Draft || release.Prerelease || !Version.TryParse(release.Tag.TrimStart('v'),out var version)) throw new InvalidDataException("The release version is invalid.");
   if(version<=Current) {MessageBox.Show(owner,$"You are up to date (version {Current.ToString(3)}).","Rulership-Pin – Updates");return;}
   var asset=release.Assets.SingleOrDefault(a=>a.Name==AssetName)??throw new InvalidDataException("The portable update package is missing.");
   ValidateAssetUrl(asset.Url,release.Tag,AssetName);
   string? hash=asset.Digest?.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)==true?asset.Digest[7..]:null;
   if(hash is null) {
    var checksum=release.Assets.SingleOrDefault(a=>a.Name==AssetName+".sha256")??throw new InvalidDataException("The update checksum is missing.");
    ValidateAssetUrl(checksum.Url,release.Tag,AssetName+".sha256");hash=(await client.GetStringAsync(checksum.Url)).Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[0];
   }
   if(hash.Length!=64 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("The update checksum is invalid.");
   if(MessageBox.Show(owner,$"Version {version.ToString(3)} is available.\n\nDownload and install it? Rulership will restart and pinned previews will close. Your settings will be kept.","Rulership-Pin – Update available",MessageBoxButton.YesNo,MessageBoxImage.Information)!=MessageBoxResult.Yes) return;
   progressText=new TextBlock {Text="Downloading update…",Margin=new Thickness(0,0,0,12)};
   var panel=new StackPanel {Margin=new Thickness(18)};panel.Children.Add(progressText);panel.Children.Add(new ProgressBar {IsIndeterminate=true,Height=12});
   var cancel=new Button {Content="Cancel",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0),Padding=new Thickness(12,4,12,4)};cancel.Click+=(_,_)=>cancellation.Cancel();panel.Children.Add(cancel);
   progress=new Window {Title="Rulership-Pin – Updating",Width=340,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,Owner=owner,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
   progress.Closed+=(_,_)=>cancellation.Cancel();progress.Show();
   string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Rulership-Pin","Updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
   string archive=Path.Combine(directory,"package.zip"),stage=Path.Combine(directory,"stage");
   Report("Downloading update…");
   using(var response=await client.GetAsync(asset.Url,HttpCompletionOption.ResponseHeadersRead,cancellation.Token)) {
    response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>500_000_000) throw new InvalidDataException("The update download is too large.");
    using var input=await response.Content.ReadAsStreamAsync();using var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
    byte[] buffer=new byte[81920];long total=0;int count;
    while((count=await input.ReadAsync(buffer,cancellation.Token))>0) {total+=count;if(total>500_000_000) throw new InvalidDataException("The update download is too large.");await output.WriteAsync(buffer.AsMemory(0,count),cancellation.Token);Report($"Downloading update… {total/1048576} MB");}
   }
   Report("Verifying update…");
   await Task.Run(()=>UpdatePackage.Extract(archive,stage,hash,version.ToString(3)));
   cancellation.Token.ThrowIfCancellationRequested();
   using var process=Process.GetCurrentProcess();
   var job=new UpdateJob(archive,hash,version.ToString(3),AppContext.BaseDirectory,process.Id,process.StartTime.ToUniversalTime().Ticks);
   string jobPath=Path.Combine(directory,"job.json");File.WriteAllText(jobPath,JsonSerializer.Serialize(job));
   var start=new ProcessStartInfo(Path.Combine(stage,"Rulership-Pin.exe")) {UseShellExecute=false,WorkingDirectory=stage,CreateNoWindow=true};start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(jobPath);
   _=Process.Start(start)??throw new IOException("Could not start the update installer.");Application.Current.Shutdown();
  } catch(OperationCanceledException) { }
  catch(Exception error) {MessageBox.Show(owner,"The update could not be completed. Your installed app has not been changed.\n\n"+error.Message,"Rulership-Pin – Update error",MessageBoxButton.OK,MessageBoxImage.Warning);}
  finally {progress?.Close();item.Header="Check for updates…";item.IsEnabled=true;}
 }
 internal static int Apply(string jobPath) {
  UpdateJob? job=null;
  try {
   job=JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(jobPath))??throw new InvalidDataException("Invalid update job.");
   if(job.Version!=Current.ToString(3)) throw new InvalidDataException("The installer version does not match the release.");
   try {using var parent=Process.GetProcessById(job.ParentId);if(parent.StartTime.ToUniversalTime().Ticks==job.ParentStartTicks && !parent.WaitForExit(30000)) throw new IOException("Rulership is still running. Close it and try again.");} catch(ArgumentException) { }
   string payload=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(jobPath))!,"payload");
   var manifest=UpdatePackage.Extract(job.Archive,payload,job.Hash,job.Version);
   using(var mutex=new Mutex(false,"Local\\RulershipPin.DesktopPet")) {
    bool acquired;try {acquired=mutex.WaitOne(0);}catch(AbandonedMutexException) {acquired=true;}
    if(!acquired) throw new IOException("Another Rulership instance is running. Close it before installing an update.");
    try {UpdatePackage.Install(payload,job.Target,manifest);}finally {mutex.ReleaseMutex();}
   }
   Process.Start(new ProcessStartInfo(Path.Combine(job.Target,"Rulership-Pin.exe")) {UseShellExecute=true,WorkingDirectory=job.Target});return 0;
  } catch(Exception error) {
   string message="The update could not be installed.\n\n"+error.Message;
   try {File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(jobPath))!,"update-error.txt"),error.ToString());} catch { }
   MessageBox.Show(message,"Rulership-Pin – Update error",MessageBoxButton.OK,MessageBoxImage.Warning);return 1;
  }
 }
}
