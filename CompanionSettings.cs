using System.IO;
using System.Text.Json;
namespace RulershipPin;
internal static class CompanionSettings
{
 static string PathName=>Path.Combine(AppContext.BaseDirectory,"companion-settings.json");
 sealed record Settings(double CompanionScale=1,string Model="Chibi",string? LowCortisolMusic=null,bool SpontaneousAnimations=true,bool AutomaticAdventures=true,string Greeting="Hello there <3",string? UserName=null);
 internal static string? LoadUserName()=>UserProfile.TryNormalizeName(Load().UserName,out var name)?name:null;
 internal static string LoadGreeting() {
  var settings=Load();if(UserProfile.TryNormalizeName(settings.UserName,out var name)) return UserProfile.Greeting(name);
  string greeting=settings.Greeting??"Hello there <3";return greeting.Length<=40?greeting:"Hello there <3";
 }
 internal static bool TrySaveUserName(string? value,out string message) {
  if(!UserProfile.TryNormalizeName(value,out var name)) {message="Please enter a name using 1–24 characters.";return false;}
  try {
   File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(Load() with {UserName=name,Greeting=UserProfile.Greeting(name)},new JsonSerializerOptions {WriteIndented=true}));
   File.Move(PathName+".tmp",PathName,true);message="";return true;
  } catch(Exception error) when(error is IOException or UnauthorizedAccessException) {message="Could not save your name. Check that the app folder is writable.";return false;}
 }
 internal static bool LoadAutomaticAdventures()=>Load().AutomaticAdventures;
 internal static bool TrySaveAutomaticAdventures(bool enabled,out string message) {
  try {
   File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(Load() with {AutomaticAdventures=enabled},new JsonSerializerOptions {WriteIndented=true}));
   File.Move(PathName+".tmp",PathName,true);message="";return true;
  } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {message="Could not save the animation setting. Check that the app folder is writable.";return false;}
 }
 internal static bool LoadSpontaneousAnimations()=>Load().SpontaneousAnimations;
 internal static bool TrySaveSpontaneousAnimations(bool enabled,out string message) {
  try {
   File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(Load() with {SpontaneousAnimations=enabled},new JsonSerializerOptions {WriteIndented=true}));
   File.Move(PathName+".tmp",PathName,true);message="";return true;
  } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {message="Could not save the animation setting. Check that the app folder is writable.";return false;}
 }
 internal static string? LoadMusic()=>Load().LowCortisolMusic;
 internal static bool TrySaveMusic(string path,out string message) {
  try {
   File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(Load() with {LowCortisolMusic=path},new JsonSerializerOptions {WriteIndented=true}));
   File.Move(PathName+".tmp",PathName,true);message="";return true;
  } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {message="Could not save the music setting. Check that the app folder is writable.";return false;}
 }
 static Settings Load() {
  try {return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName))??new();}
  catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException) {return new();}
 }
 internal static string LoadModel()=>Load().Model=="Original"?"Original":"Chibi";
 internal static double LoadScale() {
  try {
   var settings=JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName));
   return settings is not null && double.IsFinite(settings.CompanionScale) ? Math.Clamp(settings.CompanionScale,.5,1.75) : 1;
  }
  catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException) {return 1;}
 }
 internal static bool TrySaveScale(double scale,out string message) {
  string temporary=PathName+".tmp";
  try {
   File.WriteAllText(temporary,JsonSerializer.Serialize(Load() with {CompanionScale=Math.Clamp(scale,.5,1.75)},new JsonSerializerOptions {WriteIndented=true}));
   File.Move(temporary,PathName,true);message="";return true;
  }
  catch(Exception e) when(e is IOException or UnauthorizedAccessException) {
   message="Could not save the size setting. Check that the app folder is writable.";return false;
  }
 }
 internal static bool TrySaveModel(string model,out string message) {
  try {
   File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(Load() with {Model=model=="Original"?"Original":"Chibi"},new JsonSerializerOptions {WriteIndented=true}));
   File.Move(PathName+".tmp",PathName,true);message="";return true;
  }
  catch(Exception e) when(e is IOException or UnauthorizedAccessException) {
   message="Could not save the model setting. Check that the app folder is writable.";return false;
  }
 }
}
