namespace RulershipPin;
internal static class UserProfile
{
 internal static bool TryNormalizeName(string? value,out string name) {
  name=(value??"").Trim();return name.Length is >0 and <=24 && !name.Any(char.IsControl);
 }
 internal static string Greeting(string name)=>"Hello "+name+" <3";
 internal static string NameFromGreeting(string greeting) {
  if(!greeting.StartsWith("Hello ",StringComparison.Ordinal) || !greeting.EndsWith(" <3",StringComparison.Ordinal)) return "";
  string name=greeting[6..^3].Trim();return name=="there" || !TryNormalizeName(name,out var normalized)?"":normalized;
 }
}
