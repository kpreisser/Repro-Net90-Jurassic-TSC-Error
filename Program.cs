
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

using Jurassic;
using Jurassic.Library;

Console.WriteLine(".NET Version: " + RuntimeInformation.FrameworkDescription);

using var httpClient = new HttpClient();

string typescriptServicesCode = await (await httpClient.GetAsync("https://github.com/microsoft/TypeScript/raw/refs/tags/v4.5.5/lib/typescriptServices.js")).Content.ReadAsStringAsync();
string scriptApiEnvironmentLib =
    await (await httpClient.GetAsync("https://github.com/microsoft/TypeScript/raw/refs/tags/v4.5.5/lib/lib.es5.d.ts")).Content.ReadAsStringAsync();

string scriptApiLib = File.ReadAllText("scriptApiDeclaration.d.ts");

Console.WriteLine("Starting up TypeScript Compiler...");
var sw = Stopwatch.StartNew();

const string scriptEnvironmentApiDeclarationFileName = "scriptEnvironmentApiDeclaration.d.ts";
const string scriptApiDeclarationFileName = "scriptApiDeclaration.d.ts";

string compilerInterfaceScript = File.ReadAllText("typescript-compiler-scriptinterfaceplugin.js");

// Create a ScriptEngine and initialize it with the TypeScript Services.
var engine = new ScriptEngine() {
    ForceStrictMode = true
};

engine.Execute(typescriptServicesCode);
engine.Execute(compilerInterfaceScript);

// We can now get the functions necessary for compiling Codabix Scripts.
var tsCompilerObject = (ObjectInstance)engine.Global["CodabixTypeScriptCompiler"];
var getScriptPluginCompilerOptionsFunction =
        (FunctionInstance)tsCompilerObject["getScriptPluginCompilerOptions"];

var transpileCodeFunction = (FunctionInstance)tsCompilerObject["transpileCode"];

Console.WriteLine("TypeScript Compiler startup completed after " + sw.Elapsed + ", compiling script...");

var compilerOptions = getScriptPluginCompilerOptionsFunction!.CallLateBound(
    Undefined.Value,
    /* editorStrictnessLevel */ 0);

var libFiles = engine!.Object.Construct();

// Add the base libs.
libFiles[scriptEnvironmentApiDeclarationFileName] = scriptApiEnvironmentLib;
libFiles[scriptApiDeclarationFileName] = scriptApiLib;

var resultObject = (ObjectInstance)transpileCodeFunction.CallLateBound(
    Undefined.Value,
    /* scriptContent */ string.Empty,
    "script1.ts",
    libFiles,
    compilerOptions);


var outputTextValue = resultObject["outputText"];

if (!ScriptValueIsString(outputTextValue)) {
    string diagnostic = string.Empty;

    var errors = (ObjectInstance)resultObject["errors"]!;
    int errorsLength = Convert.ToInt32(errors["length"]);

    for (int i = 0; i < errorsLength; i++) {
        var errorObj = (ObjectInstance)errors[i];
        var errStart = errorObj["start"];

        if (!ScriptValueIsUndefined(errStart)) {
            var tsObj = (ObjectInstance)engine.Global["ts"];
            var lineAndCharObj = (ObjectInstance)((FunctionInstance)tsObj["getLineAndCharacterOfPosition"])
                    .CallLateBound(tsObj, errorObj["file"], errStart);
            var line = Convert.ToInt32(lineAndCharObj["line"]);

            diagnostic += $"{((ObjectInstance)errorObj["file"])["fileName"]}: Line {line.ToString(CultureInfo.InvariantCulture)}: ";
        }

        var messageText = errorObj["messageText"];
        if (!ScriptValueIsString(messageText)) {
            // ts.DiagnosticMessageChain
            messageText = ((ObjectInstance)messageText)["messageText"];
        }

        diagnostic += messageText.ToString();

        if (i < errorsLength - 1)
            diagnostic += "\r\n";
    }

    throw new InvalidOperationException($"Compilation of TypeScript code failed after {sw.Elapsed}:\r\n{diagnostic}");
}

Console.WriteLine($"Compilation succeeded after {sw.Elapsed}.");
Console.ReadKey();

static bool ScriptValueIsString([NotNullWhen(true)] object? value)
{
    return value is string or ConcatenatedString;
}

static bool ScriptValueIsUndefined([NotNullWhen(false)] object? value)
{
    return value is null or Undefined;
}

static bool ScriptValueIsNullOrUndefined([NotNullWhen(false)] object? value)
{
    return value is null or Undefined or Null;
}