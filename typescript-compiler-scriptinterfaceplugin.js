"use strict";

var MyTypeScriptCompiler = (function () {
    var libFileName = "lib.d.ts";

    return {
        getScriptPluginCompilerOptions() {
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

                // Explicitely disallow unreachable code, even in "Low" mode.
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
                        return "";
                    else if (fileName in preparedLibFiles)
                        return preparedLibFiles[fileName];
                    else
                        throw new Error("File not found");
                },
                writeFile: function(name, text) {
                    var endSourceMap = ".js.map";
                    var endDeclaration = ".d.ts";
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
                    return "";
                },
                getNewLine: function () {
                    return "\n"
                },
                fileExists: function (fileName) {
                    return fileName === inputFileName || fileName === libFileName;
                },
                readFile: function () {
                    return "";
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