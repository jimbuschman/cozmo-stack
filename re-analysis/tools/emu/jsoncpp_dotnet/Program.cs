using System.Globalization;
using System.Text.Json;
string? line;
while ((line=Console.ReadLine())!=null) {
 var request=JsonDocument.Parse(line).RootElement;
 string text=request.GetProperty("input").GetString()!;
 string culture=request.TryGetProperty("culture",out var c)?c.GetString()!:"";
 try {
  bool useDefault=request.TryGetProperty("default_style",out var defaultStyle)&&defaultStyle.GetBoolean();
  double value=useDefault?double.Parse(text,CultureInfo.GetCultureInfo(culture)):double.Parse(text,NumberStyles.Float,CultureInfo.GetCultureInfo(culture));
  Console.WriteLine(JsonSerializer.Serialize(new {input=text,culture,bits=((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("X16")}));
 } catch(Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new {input=text,culture,error=ex.GetType().Name})); }
}
