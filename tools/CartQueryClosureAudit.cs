using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

// Offline source analysis only. Does not load/execute game assemblies, emit a
// player, change a game source file, or declare a graph an approved edit allowlist.
public static class CartQueryClosureAudit
{
    sealed class Work
    {
        internal ISymbol Symbol;
        internal string Root;
    }

    sealed class Record
    {
        internal string Key, Symbol, Identity, Path, Kind, Hash, FileHash;
        internal int First, Last;
        internal readonly SortedSet<string> Roots = new SortedSet<string>(StringComparer.Ordinal);
    }

    sealed class Walker
    {
        internal CSharpCompilation Compilation;
        internal string Repository;
        internal Dictionary<string, string> SourceHashes;
        internal readonly Dictionary<SyntaxTree, SemanticModel> Models = new Dictionary<SyntaxTree, SemanticModel>();
        internal readonly Dictionary<string, Record> Records = new Dictionary<string, Record>(StringComparer.Ordinal);
        internal readonly SortedSet<string> Edges = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> Boundaries = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> Opens = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        internal readonly Queue<Work> Queue = new Queue<Work>();

        internal string Display(ISymbol symbol)
            => symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        internal string Identity(ISymbol symbol)
            => symbol.Kind + "/" + (symbol is IMethodSymbol method ? method.MethodKind + "/" + method.MetadataName : symbol.MetadataName) + ":" + Display(symbol);

        internal SemanticModel Model(SyntaxTree tree)
        {
            if (!Models.TryGetValue(tree, out var model)) Models.Add(tree, model = Compilation.GetSemanticModel(tree));
            return model;
        }

        internal void Enqueue(ISymbol symbol, string root)
        {
            if (symbol == null) return;
            if (symbol is IMethodSymbol method) symbol = (method.ReducedFrom ?? method).OriginalDefinition;
            else symbol = symbol.OriginalDefinition;
            Queue.Enqueue(new Work { Symbol = symbol, Root = root });
        }

        internal void TypeInitializers(INamedTypeSymbol type, string root)
        {
            if (type == null || type.DeclaringSyntaxReferences.Length == 0) return;
            foreach (var constructor in type.StaticConstructors) Enqueue(constructor, root);
        }

        internal void Link(ISymbol target, Record caller, string root, SyntaxNode site, string kind)
        {
            if (target == null) return;
            if (target is IMethodSymbol method) target = (method.ReducedFrom ?? method).OriginalDefinition;
            string name = Display(target);
            var line = site.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Edges.Add(Tsv(root, caller.Key, Rel(Repository, site.SyntaxTree.FilePath), line.ToString(CultureInfo.InvariantCulture), kind, name, Identity(target)));
            if (target is IMethodSymbol dispatch && (dispatch.IsAbstract || dispatch.IsVirtual || dispatch.ContainingType.TypeKind == TypeKind.Interface || dispatch.MethodKind == MethodKind.DelegateInvoke))
                Opens.Add(Tsv(root, caller.Key, "dispatch", kind, name));
            if (target.IsStatic || target is IMethodSymbol created && created.MethodKind == MethodKind.Constructor)
                TypeInitializers(target.ContainingType, root);
            if (target.DeclaringSyntaxReferences.Length == 0)
            {
                string assembly = target.ContainingAssembly?.Identity.Name ?? "<none>";
                string origin = SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, Compilation.Assembly) ? "source-synthesized" : "metadata";
                Boundaries.Add(Tsv(root, assembly, origin, kind, name));
                if (target is IMethodSymbol constructor && (constructor.MethodKind == MethodKind.Constructor || constructor.MethodKind == MethodKind.StaticConstructor) && constructor.IsImplicitlyDeclared && constructor.ContainingType.DeclaringSyntaxReferences.Length != 0)
                    Enqueue(constructor, root);
                return;
            }
            Enqueue(target, root);
        }

        internal void Scan(IOperation operation, Record caller, string root)
        {
            if (operation == null) return;
            if (operation is IInvocationOperation invocation) Link(invocation.TargetMethod, caller, root, operation.Syntax, "invoke");
            else if (operation is IObjectCreationOperation creation) Link(creation.Constructor, caller, root, operation.Syntax, "new");
            else if (operation is IMethodReferenceOperation method) Link(method.Method, caller, root, operation.Syntax, "method-group");
            else if (operation is IPropertyReferenceOperation property)
            {
                // Deliberately conservative. The output records getter/setter
                // dispatch separately; a reviewer must prove write-context cuts.
                Link(property.Property.GetMethod, caller, root, operation.Syntax, "property-get-unpruned");
                Link(property.Property.SetMethod, caller, root, operation.Syntax, "property-set-unpruned");
            }
            else if (operation is IFieldReferenceOperation field && field.Field.IsStatic && !field.Field.IsConst)
            {
                Link(field.Field, caller, root, operation.Syntax, "static-field");
                TypeInitializers(field.Field.ContainingType, root);
            }
            else if (operation is IConversionOperation conversion) Link(conversion.OperatorMethod, caller, root, operation.Syntax, "conversion");
            else if (operation is IBinaryOperation binary) Link(binary.OperatorMethod, caller, root, operation.Syntax, "binary-operator");
            else if (operation is IUnaryOperation unary) Link(unary.OperatorMethod, caller, root, operation.Syntax, "unary-operator");
            else if (operation is IIncrementOrDecrementOperation increment) Link(increment.OperatorMethod, caller, root, operation.Syntax, "increment-operator");
            else if (operation is ICompoundAssignmentOperation compound) Link(compound.OperatorMethod, caller, root, operation.Syntax, "compound-operator");
            if (operation is IUsingOperation || operation is IUsingDeclarationOperation)
                Opens.Add(Tsv(root, caller.Key, "implicit-using-disposal", "", operation.Syntax.Kind() + "@" +
                    (operation.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture)));
            if (operation is IAwaitOperation)
                Opens.Add(Tsv(root, caller.Key, "implicit-await-pattern", "", operation.Syntax.Kind() + "@" +
                    (operation.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1).ToString(CultureInfo.InvariantCulture)));
            if (operation.Kind == OperationKind.Invalid)
                Opens.Add(Tsv(root, caller.Key, "invalid-operation", "", operation.Syntax.ToString()));
            foreach (var child in operation.ChildOperations) Scan(child, caller, root);
        }

        internal void ScanNode(SyntaxNode node, Record caller, string root)
        {
            var model = Model(node.SyntaxTree);
            var operation = model.GetOperation(node);
            if (operation != null) Scan(operation, caller, root);
            else
            {
                // Declaration wrappers sometimes have no operation; scan the
                // exact expression/body, not every member of a containing type.
                bool found = false;
                foreach (var child in node.ChildNodes())
                {
                    var childOperation = model.GetOperation(child);
                    if (childOperation == null) continue;
                    found = true;
                    Scan(childOperation, caller, root);
                }
                if (!found && !(node is AccessorDeclarationSyntax accessor && accessor.Body == null && accessor.ExpressionBody == null) &&
                    !(node is VariableDeclaratorSyntax variable && variable.Initializer == null))
                    Opens.Add(Tsv(root, caller.Key, "no-operation-body", "", node.Kind().ToString()));
            }
            foreach (var loop in node.DescendantNodesAndSelf().OfType<CommonForEachStatementSyntax>())
            {
                var info = model.GetForEachStatementInfo(loop);
                Link(info.GetEnumeratorMethod, caller, root, loop, "foreach-get-enumerator");
                Link(info.MoveNextMethod, caller, root, loop, "foreach-move-next");
                Link(info.DisposeMethod, caller, root, loop, "foreach-dispose");
                Link(info.CurrentProperty?.GetMethod, caller, root, loop, "foreach-current");
                Link(info.ElementConversion.MethodSymbol, caller, root, loop, "foreach-conversion");
            }
        }

        internal void Visit(Work work)
        {
            var symbol = work.Symbol;
            var references = symbol.DeclaringSyntaxReferences;
            bool implicitConstructor = symbol is IMethodSymbol constructor &&
                (constructor.MethodKind == MethodKind.Constructor || constructor.MethodKind == MethodKind.StaticConstructor) && constructor.IsImplicitlyDeclared;
            if (references.Length == 0 && implicitConstructor) references = symbol.ContainingType.DeclaringSyntaxReferences;
            if (references.Length == 0) return;
            foreach (var reference in references)
            {
                var node = reference.GetSyntax();
                string path = Rel(Repository, node.SyntaxTree.FilePath);
                string display = Display(symbol);
                string identity = Identity(symbol);
                string key = path + ":" + node.Span.Start.ToString(CultureInfo.InvariantCulture) + ":" + identity;
                if (!Records.TryGetValue(key, out var record))
                {
                    var span = node.GetLocation().GetLineSpan();
                    record = new Record { Key = key, Symbol = display, Identity = identity, Path = path, Kind = symbol.Kind + "/" + node.Kind(), First = span.StartLinePosition.Line + 1, Last = span.EndLinePosition.Line + 1, Hash = Sha(Normalize(node.ToString())), FileHash = SourceHashes[node.SyntaxTree.FilePath] };
                    Records.Add(key, record);
                }
                record.Roots.Add(work.Root);
                if (!Visited.Add(work.Root + "|" + key)) continue;
                if (implicitConstructor && node is TypeDeclarationSyntax)
                {
                    ScanInitializers((IMethodSymbol)symbol, record, work.Root);
                }
                else
                {
                    ScanNode(node, record, work.Root);
                    if (symbol is IMethodSymbol explicitConstructor &&
                        (explicitConstructor.MethodKind == MethodKind.Constructor || explicitConstructor.MethodKind == MethodKind.StaticConstructor))
                        ScanInitializers(explicitConstructor, record, work.Root);
                }
            }
        }

        internal void ScanInitializers(IMethodSymbol constructor, Record caller, string root)
        {
            bool isStatic = constructor.MethodKind == MethodKind.StaticConstructor;
            foreach (var reference in constructor.ContainingType.DeclaringSyntaxReferences)
            {
                if (!(reference.GetSyntax() is TypeDeclarationSyntax type)) continue;
                foreach (var member in type.Members)
                {
                    if (member is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.StaticKeyword) == isStatic)
                        foreach (var variable in field.Declaration.Variables)
                            if (variable.Initializer != null) ScanNode(variable.Initializer.Value, caller, root);
                    if (member is PropertyDeclarationSyntax property && property.Initializer != null && property.Modifiers.Any(SyntaxKind.StaticKeyword) == isStatic)
                        ScanNode(property.Initializer.Value, caller, root);
                }
            }
            if (constructor.IsImplicitlyDeclared && !isStatic && constructor.ContainingType.TypeKind != TypeKind.Struct)
            {
                var baseType = constructor.ContainingType.BaseType;
                if (baseType != null)
                {
                    // A unique exact parameterless base constructor wins the
                    // implicit base() call. Optional/params overload selection
                    // stays OPEN rather than choosing a plausible constructor.
                    var exact = baseType.InstanceConstructors.Where(candidate => candidate.Parameters.Length == 0).ToArray();
                    if (exact.Length == 1)
                        Link(exact[0], caller, root, constructor.ContainingType.DeclaringSyntaxReferences[0].GetSyntax(), "implicit-base-parameterless");
                    else Opens.Add(Tsv(root, caller.Key, "implicit-base-constructor", "", Display(constructor)));
                }
            }
        }
    }

    static string Normalize(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n");
    static string Sha(string text)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }
    static string FileSha(string path)
    {
        using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }
    static string ByteSha(byte[] bytes)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }
    static void VerifyUnchanged(IDictionary<string, string> inputs)
    {
        foreach (var pair in inputs)
            if (!File.Exists(pair.Key) || FileSha(pair.Key) != pair.Value)
                throw new IOException("Audit input changed; discard this analysis: " + pair.Key);
    }
    static string Rel(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    static string Tsv(params string[] values) => string.Join("\t", values.Select(value => (value ?? "").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n")));
    static void Write(string folder, string name, IEnumerable<string> rows) => File.WriteAllText(Path.Combine(folder, name), string.Join("\n", rows) + "\n", new UTF8Encoding(false));
    static void WriteTable(string folder, string name, string header, IEnumerable<string> rows)
    {
        int width = header.Split('\t').Length;
        var captured = rows.ToArray();
        if (captured.Any(row => row.Split('\t').Length != width))
            throw new InvalidDataException("Audit table schema mismatch: " + name);
        Write(folder, name, new[] { header }.Concat(captured));
    }

    static void VerifyWalker(string repository, string output, CSharpParseOptions parse, IEnumerable<MetadataReference> references)
    {
        // Independent language-semantic probes: this text is parsed and bound,
        // never emitted or executed and contains no fabricated game fixtures.
        const string fixture = @"
namespace ClosureAuditFixture {
 interface IRunner { int Run(); }
 class Base { public Base() { Leaf.BaseCall(); } }
 class Host : Base {
  static readonly int shared = Leaf.StaticInit();
  readonly int local = Leaf.InstanceInit();
  public Host() { }
  public int Value { get { return Leaf.Getter(); } set { Leaf.Setter(); } }
  public int Go(IRunner runner) {
   var child = new Implicit();
   System.Func<int> callback = Leaf.MethodGroup;
   Value = Leaf.DirectCall() + shared + local;
   var sum = new Number() + new Number();
   return callback() + runner.Run() + Value;
  }
 }
 class ImplicitBase { public ImplicitBase() { Leaf.ImplicitBaseCall(); } }
 class Implicit : ImplicitBase {
  static readonly int shared = Leaf.ImplicitStaticInit();
  readonly int local = Leaf.ImplicitInstanceInit();
 }
 class OptionalBase { protected OptionalBase(int value = 0) { Leaf.OptionalBaseCall(); } }
 class OptionalChild : OptionalBase { }
 class Lifetime : System.IDisposable { public void Dispose() { Leaf.UsingDisposeCall(); } }
 struct Number { public static Number operator +(Number a, Number b) { Leaf.Operator(); return a; } }
 class Sequence { public Enumerator GetEnumerator() { Leaf.Enumerate(); return new Enumerator(); } }
 ref struct Enumerator {
  public int Current { get { return Leaf.Current(); } }
  public bool MoveNext() { Leaf.Move(); return false; }
  public void Dispose() { Leaf.DisposeCall(); }
 }
 static class Entry {
  public static int Start(IRunner runner) {
   var host = new Host();
   var optional = new OptionalChild();
   foreach (var value in new Sequence()) host.Value = value;
   using (var lifetime = new Lifetime()) Leaf.UsingBody();
   return host.Go(runner);
  }
 }
 static class Leaf {
  public static int BaseCall() { return 1; }
  public static int StaticInit() { return 1; }
  public static int InstanceInit() { return 1; }
  public static int ImplicitStaticInit() { return 1; }
  public static int ImplicitInstanceInit() { return 1; }
  public static int ImplicitBaseCall() { return 1; }
  public static int OptionalBaseCall() { return 1; }
  public static int Getter() { return 1; }
  public static int Setter() { return 1; }
  public static int MethodGroup() { return 1; }
  public static int DirectCall() { return 1; }
  public static int Operator() { return 1; }
  public static int Enumerate() { return 1; }
  public static int Current() { return 1; }
  public static int Move() { return 1; }
  public static int DisposeCall() { return 1; }
  public static int UsingBody() { return 1; }
  public static int UsingDisposeCall() { return 1; }
 }
}
";
        string path = Path.Combine(output, "semantic-fixture.cs");
        File.WriteAllText(path, fixture, new UTF8Encoding(false));
        var tree = CSharpSyntaxTree.ParseText(fixture, parse, path, Encoding.UTF8);
        var compilation = CSharpCompilation.Create("ClosureAuditFixture", new[] { tree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(error => error.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException("Audit fixture failed semantic compilation: " + string.Join(";", errors.Select(error => error.ToString())));
        var fixtureHashes = new Dictionary<string, string>(StringComparer.Ordinal) { [path] = FileSha(path) };
        var walker = new Walker { Compilation = compilation, Repository = repository, SourceHashes = fixtureHashes };
        var entry = compilation.GetTypeByMetadataName("ClosureAuditFixture.Entry").GetMembers("Start").OfType<IMethodSymbol>().Single();
        walker.Enqueue(entry, "language-probe");
        while (walker.Queue.Count != 0) walker.Visit(walker.Queue.Dequeue());
        WriteTable(output, "self-check-open.tsv", Tsv("root", "caller", "reason", "kind", "detail"), walker.Opens);
        var evidence = new List<string>();
        foreach (string method in new[] { "BaseCall", "StaticInit", "InstanceInit", "ImplicitStaticInit", "ImplicitInstanceInit", "ImplicitBaseCall", "Getter", "Setter", "MethodGroup", "DirectCall", "Operator", "Enumerate", "Current", "Move", "DisposeCall", "UsingBody" })
        {
            if (!walker.Records.Values.Any(record => record.Symbol == "ClosureAuditFixture.Leaf." + method + "()"))
                throw new InvalidOperationException("Audit walker omitted required language-semantic route: " + method);
            evidence.Add("PASS " + method);
        }
        if (!walker.Opens.Any(row => row.Contains("ClosureAuditFixture.IRunner.Run()")))
            throw new InvalidOperationException("Audit walker concealed unresolved interface dispatch.");
        if (!walker.Opens.Any(row => row.Contains("System.Func<TResult>.Invoke()")))
            throw new InvalidOperationException("Audit walker concealed unresolved delegate dispatch.");
        if (!walker.Opens.Any(row => row.Contains("implicit-base-constructor") && row.Contains("ClosureAuditFixture.OptionalChild.OptionalChild()")) ||
            walker.Records.Values.Any(record => record.Symbol == "ClosureAuditFixture.Leaf.OptionalBaseCall()"))
            throw new InvalidOperationException("Audit walker guessed optional base-constructor dispatch instead of keeping it OPEN.");
        evidence.Add("PASS interface/delegate dispatch remains explicitly OPEN");
        evidence.Add("PASS optional base-constructor dispatch stays OPEN without a guessed implementation");
        if (!walker.Opens.Any(row => row.Contains("implicit-using-disposal")))
            throw new InvalidOperationException("Audit walker concealed implicit using-disposal lowering.");
        evidence.Add("PASS implicit using-disposal lowering remains explicitly OPEN");
        File.AppendAllText(path, "\n// deliberate probe drift\n", new UTF8Encoding(false));
        bool rejectedDrift = false;
        try { VerifyUnchanged(fixtureHashes); }
        catch (IOException) { rejectedDrift = true; }
        finally { File.WriteAllText(path, fixture, new UTF8Encoding(false)); }
        if (!rejectedDrift) throw new InvalidOperationException("Audit input-byte drift was not rejected.");
        VerifyUnchanged(fixtureHashes);
        evidence.Add("PASS changed input bytes are rejected; restored fixture bytes match the original capture");
        bool rejectedSchema = false;
        try { WriteTable(output, "invalid-table-probe.tsv", Tsv("first", "second"), new[] { "missing-column" }); }
        catch (InvalidDataException) { rejectedSchema = true; }
        if (!rejectedSchema || File.Exists(Path.Combine(output, "invalid-table-probe.tsv")))
            throw new InvalidOperationException("Audit table schema mismatch was not rejected before output.");
        evidence.Add("PASS malformed table records are rejected before writing evidence");
        evidence.Add("Source parsed and bound only; no fixture assembly emitted or executed.");
        Write(output, "self-check.txt", evidence);
    }

    public static string Run(string repository, string responseFile, string output)
    {
        repository = Path.GetFullPath(repository);
        string game = Path.Combine(repository, "Game");
        output = Path.GetFullPath(output);
        string allowed = Path.Combine(repository, "reports", "save-refactor") + Path.DirectorySeparatorChar;
        if (!output.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Audit output must remain under reports/save-refactor.", nameof(output));
        Directory.CreateDirectory(output);
        string responseHash = FileSha(responseFile);
        var args = CSharpCommandLineParser.Default.Parse(new[] { "-noconfig", "-nostdlib+", "@" + Path.GetFullPath(responseFile) }, game, null);
        if (args.Errors.Any(error => error.Severity == DiagnosticSeverity.Error)) throw new InvalidOperationException(string.Join("\n", args.Errors));
        var sourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var trees = args.SourceFiles.Select(file =>
        {
            byte[] bytes = File.ReadAllBytes(file.Path);
            sourceHashes.Add(file.Path, ByteSha(bytes));
            string text;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), true)) text = reader.ReadToEnd();
            return CSharpSyntaxTree.ParseText(text, args.ParseOptions, file.Path, Encoding.UTF8);
        }).ToArray();
        var references = new List<MetadataReference>();
        var referenceRows = new SortedSet<string>(StringComparer.Ordinal);
        var referenceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in args.MetadataReferences)
        {
            string path = Path.IsPathRooted(reference.Reference) ? reference.Reference : Path.Combine(game, reference.Reference);
            if (!File.Exists(path)) throw new FileNotFoundException("A compiler reference cannot be resolved; no guessed framework fallback is allowed.", path);
            string hash = FileSha(path);
            references.Add(MetadataReference.CreateFromFile(path, reference.Properties));
            referenceHashes[path] = hash;
            referenceRows.Add(Tsv(hash, Path.GetFullPath(path)));
        }
        var compilation = CSharpCompilation.Create(args.CompilationName, trees, references, args.CompilationOptions);
        var errors = compilation.GetDiagnostics().Where(error => error.Severity == DiagnosticSeverity.Error).Select(error => error.ToString()).OrderBy(text => text, StringComparer.Ordinal).ToArray();
        Write(output, "compilation-errors.txt", errors);
        Write(output, "source-sha256.tsv", trees.Select(tree => Tsv(sourceHashes[tree.FilePath], Rel(repository, tree.FilePath))).OrderBy(text => text, StringComparer.Ordinal));
        Write(output, "reference-sha256.tsv", referenceRows);
        if (errors.Length != 0) throw new InvalidOperationException("Semantic audit has " + errors.Length + " compiler errors. Inspect compilation-errors.txt; no graph acceptance is claimed.");
        VerifyWalker(repository, output, args.ParseOptions, references);
        var walker = new Walker { Compilation = compilation, Repository = repository, SourceHashes = sourceHashes };
        var rootRows = new List<string>();
        foreach (var target in new[] {
            new[] { "DragLab.Domain.CarInspection", "PrepareCar", "physical-calculation" },
            new[] { "DragLab.Domain.CarInspection", "CalculatePrepared", "physical-calculation" },
            new[] { "DragLab.Domain.InspectionBuild", "Capture", "legacy-capture-context" },
            new[] { "DragLab.Domain.Mechanics.PartCart", "Quote", "quote-context" },
            new[] { "DragLab.Domain.Mechanics.CartApplyWorkingSet+ScopedCartCatalogRead", "Resolve", "typed-resolve-seam-context" },
            new[] { "DragLab.Domain.ConcreteInstalledBuildInput", "Inspection", "typed-inspection-seam-context", "DragLab.Domain.Mechanics.ICartCatalogRead" }
        })
        {
            var type = compilation.GetTypeByMetadataName(target[0]) ?? throw new InvalidOperationException("Missing exact root type: " + target[0]);
            var methods = type.GetMembers(target[1]).OfType<IMethodSymbol>().ToArray();
            if (target.Length == 4)
                methods = methods.Where(method => method.Parameters.Length == 1 && method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == target[3]).ToArray();
            if (methods.Length != 1) throw new InvalidOperationException("Expected one exact root overload: " + target[0] + "." + target[1]);
            walker.Enqueue(methods[0], target[2]);
            rootRows.Add(Tsv(target[2], walker.Display(methods[0]), walker.Identity(methods[0])));
        }
        while (walker.Queue.Count != 0) walker.Visit(walker.Queue.Dequeue());
        VerifyUnchanged(sourceHashes);
        VerifyUnchanged(referenceHashes);
        if (FileSha(responseFile) != responseHash) throw new IOException("Compiler response file changed; discard this analysis.");
        WriteTable(output, "roots.tsv", Tsv("root", "symbol", "symbol_id"), rootRows);
        var memberHeader = Tsv("key", "roots", "symbol", "symbol_id", "path", "first", "last", "kind", "declaration_sha256", "file_sha256");
        Func<Record, string> memberRow = record => Tsv(record.Key, string.Join(",", record.Roots), record.Symbol, record.Identity, record.Path, record.First.ToString(CultureInfo.InvariantCulture), record.Last.ToString(CultureInfo.InvariantCulture), record.Kind, record.Hash, record.FileHash);
        WriteTable(output, "methods.tsv", memberHeader, walker.Records.Values.OrderBy(record => record.Key, StringComparer.Ordinal).Select(memberRow));
        WriteTable(output, "physical-members.tsv", memberHeader, walker.Records.Values.Where(record => record.Roots.Contains("physical-calculation")).OrderBy(record => record.Key, StringComparer.Ordinal).Select(memberRow));
        var physical = walker.Records.Values.Where(record => record.Roots.Contains("physical-calculation")).OrderBy(record => record.Key, StringComparer.Ordinal).ToArray();
        WriteTable(output, "physical-review-members.tsv", Tsv("path", "first", "last", "kind", "symbol_id", "declaration_sha256"),
            physical.Select(record => Tsv(record.Path, record.First.ToString(CultureInfo.InvariantCulture), record.Last.ToString(CultureInfo.InvariantCulture), record.Kind, record.Identity, record.Hash)));
        WriteTable(output, "physical-review-files.tsv", Tsv("path", "file_sha256"),
            physical.GroupBy(record => record.Path, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Tsv(group.Key, group.Select(record => record.FileHash).Distinct(StringComparer.Ordinal).Single())));
        WriteTable(output, "edges.tsv", Tsv("root", "caller", "site_path", "site_line", "kind", "callee", "callee_id"), walker.Edges);
        WriteTable(output, "metadata-boundaries.tsv", Tsv("root", "assembly", "origin", "kind", "symbol"), walker.Boundaries);
        WriteTable(output, "open-dispatch.tsv", Tsv("root", "caller", "reason", "kind", "detail"), walker.Opens);
        string summary = "sources=" + trees.Length + ";references=" + references.Count + ";members=" + walker.Records.Count + ";edges=" + walker.Edges.Count + ";noSourceDeclarationBoundaries=" + walker.Boundaries.Count + ";OPEN=" + walker.Opens.Count;
        Write(output, "summary.txt", new[] { "UNPRUNED SEMANTIC AUDIT ONLY; NOT A CLOSED CALL GRAPH OR EDIT AUTHORIZATION", summary, "Branches, virtual/interface/delegate dispatch, optional base constructors, static initialization timing, reflection, iterator machinery and metadata boundaries require explicit review. No source generator/analyzer/ILPP is executed. No game assembly is loaded or executed. No game source is changed; compilation.Emit is never called. The language probe writes only its ignored report fixture.", "Source/reference/response endpoint byte hashes match their captured inputs. Run under an external source freeze; endpoint hashes are not a concurrent A-to-B-to-A guarantee.", "Response SHA256=" + responseHash });
        return summary;
    }
}
