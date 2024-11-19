
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
    await (await httpClient.GetAsync("https://github.com/microsoft/TypeScript/raw/refs/tags/v4.5.5/lib/lib.es5.d.ts")).Content.ReadAsStringAsync() + "\r\n" +
    await (await httpClient.GetAsync("https://github.com/microsoft/TypeScript/raw/refs/tags/v4.5.5/lib/lib.es2015.promise.d.ts")).Content.ReadAsStringAsync();

string scriptApiLib = await (await httpClient.GetAsync("https://github.com/Traeger-GmbH/codabix-samples/raw/5c328111ac8b49cc5b90f297dad7d64d1448f137/scripts/scripts-api.d.ts")).Content.ReadAsStringAsync();

Console.WriteLine("Starting up TypeScript Compiler...");
var sw = Stopwatch.StartNew();

const string scriptEnvironmentApiDeclarationFileName = "scriptEnvironmentApiDeclaration.d.ts";
const string scriptApiDeclarationFileName = "scriptApiDeclaration.d.ts";

string compilerInterfaceScript = @"
""use strict"";

// Note: The code from this file was copied (and changed to be ES5-compatible) from
// file ""ext/typescript-compiler.ts"" (Codabix.Config).

var CodabixTypeScriptCompiler = (function () {
    var libFileName = ""lib.d.ts"";

    var ScriptEditorStrictness = {
        Low: 0,
        Medium: 50,
        High: 100
    };

    return {
        getScriptPluginCompilerOptions(editorStrictnessLevel) {
            var opts = {
                // Need to force strict mode because the .NET JS Runtime (Jurassic) also forces
                // strict mode.
                alwaysStrict: true,

                // strictNullChecks are only disabled by default because of backwards-compatibility,
                // but should always be enabled in new codebases.
                strictNullChecks: true,

                // Enable contravariant (instead of bivariant) comparison for function parameter types.
                strictFunctionTypes: true,

                // Ensure properties are initialized in a constructor
                strictPropertyInitialization: true,

                // Ensure Function.bind(), .call() and .apply() are strongly typed
                strictBindCallApply: true,

                // TODO: Check whether we should enable this (for all levels) in a future version.
                // While it does not fall under the 'strict' family options, the intent of the
                // TypeScript team is that this option should have been enabled by default, but
                // because of existing code (and people that learned the existing type system) it
                // is not enabled by default.
                //exactOptionalPropertyTypes: true,

                // Explicitely disallow unreachable code, even in ""Low"" mode.
                // This was alread the default in earlier TS versions but in the current one
                // it is not enabled.However, not setting this causes confusing flow analysis
                // results - see https://github.com/Microsoft/TypeScript/issues/26914.
                allowUnreachableCode: false,

                // We target ES5. Once Jurassic supports ECMAScript 2015, set this to ES2015.
                target: ts.ScriptTarget.ES5,

                // Create source maps and declaration files.
                sourceMap: true,
                declaration: true,

                // Don't emit on error because we don't allow the user to save on error.
                noEmitOnError: true,

                // Remove comments to get a compact compiled script.
                removeComments: true,

                // Required for the monaco editor
                allowNonTsExtensions: true,
                // Don't resolve imports because we cannot provide them.
                noResolve: true,
                module: ts.ModuleKind.None,
            };

            // Adjust the options according to the Editor Strictness Level.
            if (editorStrictnessLevel >= ScriptEditorStrictness.Medium) {
                // Add medium strict options. ""noImplicitAny"" and ""noImplicitThis"" are enabled here,
                // to make it easier for JS developers that don't know how to declare types in
                // TypeScript when using level ""Low"".
                opts.noImplicitAny = true;
                opts.noImplicitThis = true;
                // Raise an error if no ""override"" modifier is present for an overridden method.
                opts.noImplicitOverride = true;
                // Use unknown instead of any as type for the 'catch' variable.
                opts.useUnknownInCatchVariables = true;
                opts.noFallthroughCasesInSwitch = true;
            }

            if (editorStrictnessLevel >= ScriptEditorStrictness.High) {
                // Add most strict options.            
                opts.noImplicitReturns = true;
                opts.noUnusedLocals = true;
                opts.noUnusedParameters = true;
                // Properties accessed by index signature cannot use the dot syntax.
                opts.noPropertyAccessFromIndexSignature = true;
            }

            return opts;
        },
        transpileCode(input, inputFileName, libFiles, compilerOptions) {
            var sourceFile = ts.createSourceFile(inputFileName, input, compilerOptions.target);

            // Prepare the lib files and also add them to the compiler options.
            var preparedLibFiles = {};
            var compilerOptionsLibs = compilerOptions.lib = [];

            for (var key in libFiles) {
                var preparedLibFile = ts.createSourceFile(key, libFiles[key], compilerOptions.target);
                preparedLibFiles[key] = preparedLibFile;

                compilerOptionsLibs.push(key);
            }

            // Output
            var outputText;
            var outputSourceMap;
            var outputDeclaration;

            // Create a compilerHost object to allow the compiler to read and write virtual files
            var compilerHost = {
                getDirectories: function () {
                    return [];
                },
                getSourceFile: function(fileName) {
                    if (fileName == inputFileName)
                        return sourceFile;
                    else if (fileName == libFileName)
                        return """";
                    else if (fileName in preparedLibFiles)
                        return preparedLibFiles[fileName];
                    else
                        throw new Error(""File not found"");
                },
                writeFile: function(name, text) {
                    var endSourceMap = "".js.map"";
                    var endDeclaration = "".d.ts"";
                    if (name.length >= endSourceMap.length && name.substring(name.length - endSourceMap.length) == endSourceMap)
                        outputSourceMap = text;
                    else if (name.length >= endDeclaration.length && name.substring(name.length - endDeclaration.length) == endDeclaration)
                        outputDeclaration = text;
                    else
                        outputText = text;
                },
                getDefaultLibFileName: function () {
                    return libFileName;
                },
                useCaseSensitiveFileNames: function () {
                    return true;
                },
                getCanonicalFileName: function (fileName) {
                    return fileName;
                },
                getCurrentDirectory: function () {
                    return """";
                },
                getNewLine: function () {
                    return ""\n""
                },
                fileExists: function (fileName) {
                    return fileName === inputFileName || fileName === libFileName;
                },
                readFile: function () {
                    return """";
                },
                directoryExists: function () {
                    return true;
                }
            };

            const program = ts.createProgram([inputFileName], compilerOptions, compilerHost);

            // Emit
            let emitResult = program.emit();

            // Need to get diagnostics. In our config we only display them as errors if outputText is undefined.
            let allDiagnostics = emitResult.diagnostics.slice();
            if (allDiagnostics.length == 0)
                allDiagnostics = ts.getPreEmitDiagnostics(program).slice();

            return {
                outputText: outputText,
                outputSourceMap: outputSourceMap,
                outputDeclaration: outputDeclaration,
                errors: allDiagnostics
            };
        }
    };
})();
";


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
    "",
    "script1.ts",
    libFiles,
    compilerOptions);


var outputTextValue = resultObject["outputText"];

if (!ScriptValueIsString(outputTextValue)) {
    string diagnostic = string.Empty;
    var errors = (ObjectInstance)resultObject["errors"]!;
    var firstError = errors[0];

    if (!ScriptValueIsNullOrUndefined(firstError)) {
        var firstErrorObj = (ObjectInstance)firstError;
        var errStart = firstErrorObj["start"];

        if (!ScriptValueIsUndefined(errStart)) {
            var tsObj = (ObjectInstance)engine.Global["ts"];
            var lineAndCharObj = (ObjectInstance)((FunctionInstance)tsObj["getLineAndCharacterOfPosition"])
                    .CallLateBound(tsObj, firstErrorObj["file"], errStart);
            var line = Convert.ToInt32(lineAndCharObj["line"]);

            diagnostic = $"{((ObjectInstance)firstErrorObj["file"])["fileName"]}: Line {line.ToString(CultureInfo.InvariantCulture)}: ";
        }

        var messageText = firstErrorObj["messageText"];
        if (!ScriptValueIsString(messageText)) {
            // ts.DiagnosticMessageChain
            messageText = ((ObjectInstance)messageText)["messageText"];
        }

        diagnostic += messageText.ToString();
    }

    throw new InvalidOperationException($"Compilation of TypeScript code failed after {sw.Elapsed}: {diagnostic}");
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