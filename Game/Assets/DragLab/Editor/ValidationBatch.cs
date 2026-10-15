using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DragLab.Editor
{
    /// <summary>
    /// Runs selected verification entry points sequentially inside a single Unity
    /// -batchmode session; the manifest comes from tools/Run-Validation.ps1.
    /// </summary>
    public static class ValidationBatch
    {
        const string ManifestVariable = "DRAGLAB_VALIDATION_MANIFEST";

        // Invoked using -executeMethod DragLab.Editor.ValidationBatch.Run.
        public static void Run()
        {
            var manifest = Environment.GetEnvironmentVariable(ManifestVariable);
            if (string.IsNullOrWhiteSpace(manifest) || !File.Exists(manifest))
                throw new InvalidOperationException("Validation manifest missing. Use tools/Run-Validation.ps1.");

            var lines = File.ReadAllLines(manifest, Encoding.UTF8);
            if (lines.Length < 2 || (lines[0] != "#continue=0" && lines[0] != "#continue=1"))
                throw new InvalidOperationException("Invalid validation manifest header.");
            bool continueOnFailure = lines[0] == "#continue=1";
            string resultFile = manifest + ".results.tsv";
            File.WriteAllText(resultFile, "", new UTF8Encoding(false));

            int passed = 0, failed = 0;
            foreach (string line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var fields = line.Split('\t');
                if (fields.Length != 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
                    throw new InvalidOperationException("Malformed validation entry in " + manifest);

                string action = fields[0], methodName = fields[1];
                Exception error = null;
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    UnityEngine.Debug.Log("DRAGLAB VALIDATION START: " + action);
                    Invoke(methodName);
                    passed++;
                    UnityEngine.Debug.Log("DRAGLAB VALIDATION PASS: " + action);
                }
                catch (Exception thrown)
                {
                    error = thrown is TargetInvocationException && thrown.InnerException != null
                        ? thrown.InnerException : thrown;
                    failed++;
                    UnityEngine.Debug.LogError("DRAGLAB VALIDATION FAIL: " + action + " / " + error);
                }
                finally
                {
                    stopwatch.Stop();
                    string encodedError = Convert.ToBase64String(Encoding.UTF8.GetBytes(error == null ? "" : error.ToString()));
                    string record = (error == null ? "PASS" : "FAIL") + "\t" + action + "\t"
                        + stopwatch.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + encodedError + Environment.NewLine;
                    // Flush after every test: even a crash retains earlier results.
                    File.AppendAllText(resultFile, record, new UTF8Encoding(false));
                }
                if (error != null && !continueOnFailure) break;
            }

            if (failed > 0)
                throw new InvalidOperationException("DragLab validation batch failed: "
                    + failed + " failed, " + passed + " passed. Results: " + resultFile);
            UnityEngine.Debug.Log("DRAGLAB VALIDATION BATCH PASS: " + passed + " tests. Results: " + resultFile);
        }

        static void Invoke(string qualifiedName)
        {
            int dot = qualifiedName.LastIndexOf('.');
            if (dot <= 0 || dot == qualifiedName.Length - 1)
                throw new InvalidOperationException("Invalid verification method: " + qualifiedName);
            string typeName = qualifiedName.Substring(0, dot);
            string methodName = qualifiedName.Substring(dot + 1);
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null) throw new MissingMemberException("Missing verification class: " + typeName);
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(typeName, methodName + " (public static void, no arguments)");
            method.Invoke(null, null);
        }
    }
}
