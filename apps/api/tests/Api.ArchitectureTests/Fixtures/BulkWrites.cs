using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Tenancy;
using Tenancy.Caching;

namespace Api.ArchitectureTests.Fixtures;

// Finds the writes that EF Core's change tracker does not see, a bulk statement (ExecuteUpdate, ExecuteDelete) or raw SQL, that change
// a table of a cached entity type, and that do not remove the tag of that type in the same method (TenantDbContext.RemoveCacheTagsAfterCommit,
// with the tag's constant from the module's CacheTags). Each finding is "file:line: table". A bulk statement whose rows it cannot tell
// is a finding too: a write the test cannot read is not known to be safe.
internal static partial class BulkWrites
{
    private const string Removal = "RemoveCacheTagsAfterCommit";

    private static readonly string[] BulkMethods = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

    private static readonly string[] RawMethods =
    [
        "ExecuteSql", "ExecuteSqlAsync", "ExecuteSqlRaw", "ExecuteSqlRawAsync", "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync",
        "SqlQuery", "SqlQueryRaw", "FromSql", "FromSqlRaw", "FromSqlInterpolated", "CreateCommand",
    ];

    // Columns of a cached table that no cached read reads, which an UPDATE may change without a removal. users.membership_count is the
    // user's count of memberships, derived data that the platform's list reads (R13); no cached read holds it.
    private static readonly Dictionary<string, string[]> UncachedColumns = new() { ["control_plane.users"] = ["membership_count"] };

    // Every table of a cached entity type, with the constant of its tag: the module's CacheTags field whose value is the tag's kind.
    public static IReadOnlyList<CachedTable> CachedTables() =>
    [
        .. ModuleDbContexts.Types()
            .Where(type => type.IsSubclassOf(typeof(TenantDbContext)))
            .SelectMany(type =>
            {
                using var context = ModuleDbContexts.CreateAsDotnetEfDoes(type);
                var tagTable = (IReadOnlyList<CacheTagSource>)typeof(TenantDbContext)
                    .GetProperty("CacheTagSources", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!;
                return tagTable
                    .Select(source => (Entity: context.Model.FindEntityType(source.Entity)!, source.Kind))
                    .Select(row => new CachedTable($"{row.Entity.GetSchema()}.{row.Entity.GetTableName()}", row.Entity.ClrType, KindConstantOf(type.Assembly, row.Kind)))
                    .ToList();
            }),
    ];

    public static IEnumerable<string> WithoutRemoval(string file, string source, IReadOnlyList<CachedTable> tables)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var calls = root.DescendantNodes().OfType<InvocationExpressionSyntax>().SelectMany(invocation => FindingsOf(invocation, tables));
        var commands = root.DescendantNodes()
            .Where(node => node is ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax { Identifier.Text: "NpgsqlCommand" } }
                || node is AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax { Name.Identifier.Text: "CommandText" } })
            .SelectMany(node => CommandFindingsOf(node, tables));
        return calls.Concat(commands).Select(finding => $"{file}:{finding.Line}: {finding.Table}");
    }

    private static IEnumerable<(int Line, string Table)> FindingsOf(InvocationExpressionSyntax invocation, IReadOnlyList<CachedTable> tables)
    {
        var name = MethodName(invocation);
        var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var method = invocation.Ancestors().FirstOrDefault(node => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax or PropertyDeclarationSyntax);
        IEnumerable<CachedTable> written = BulkMethods.Contains(name) ? BulkTarget(invocation, tables)
            : RawMethods.Contains(name) ? RawTargets(invocation, method, tables)
            : [];

        return written
            .Where(table => !Removes(method, table))
            .Select(table => (line, table.Name));
    }

    // An ADO.NET command: the text it is created with, or the text its CommandText is given.
    private static IEnumerable<(int Line, string Table)> CommandFindingsOf(SyntaxNode node, IReadOnlyList<CachedTable> tables)
    {
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var method = node.Ancestors().FirstOrDefault(ancestor => ancestor is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax or PropertyDeclarationSyntax);
        var text = node switch
        {
            ObjectCreationExpressionSyntax { ArgumentList.Arguments: [var first, ..] } => TextOf(first.Expression, method),
            AssignmentExpressionSyntax assignment => TextOf(assignment.Right, method),
            _ => "",
        };
        return WrittenBy(text, tables).Where(table => !Removes(method, table)).Select(table => (line, table.Name));
    }

    // The table of a bulk statement: the set it starts from, a property of the type that holds the code or of a module DbContext.
    private static IEnumerable<CachedTable> BulkTarget(InvocationExpressionSyntax invocation, IReadOnlyList<CachedTable> tables)
    {
        var set = RootMember(((MemberAccessExpressionSyntax)invocation.Expression).Expression);
        var holder = invocation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;
        var candidates = ModuleDbContexts.Types()
            .Concat(Solution.AllProjects.SelectMany(project => Solution.Load(project).GetTypes()).Where(type => type.Name == holder))
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(property => property.Name == set))
            .Select(property => ElementOf(property.PropertyType))
            .Distinct()
            .ToList();

        return candidates is [{ } entity]
            ? tables.Where(table => table.Entity == entity)
            : [new CachedTable($"a set this test cannot resolve ({set})", typeof(object), "")];
    }

    // The tables a raw statement writes: the text of its SQL, or of the local it names.
    private static IEnumerable<CachedTable> RawTargets(InvocationExpressionSyntax invocation, SyntaxNode? method, IReadOnlyList<CachedTable> tables)
    {
        var sql = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } argument ? TextOf(argument, method) : "";
        return WrittenBy(sql, tables);
    }

    private static IEnumerable<CachedTable> WrittenBy(string sql, IReadOnlyList<CachedTable> tables)
    {
        return Writes().Matches(sql)
            .Where(write => !OnlyUncachedColumns(write))
            .SelectMany(write => tables.Where(table => table.Name == write.Groups["table"].Value || table.Name.EndsWith($".{write.Groups["table"].Value}", StringComparison.Ordinal)));
    }

    private static bool OnlyUncachedColumns(Match write) =>
        write.Groups["set"].Success
        && UncachedColumns.TryGetValue(write.Groups["table"].Value, out var uncached)
        && write.Groups["set"].Value.Split(',').Select(assignment => assignment.Split('=')[0].Trim()).All(uncached.Contains);

    private static string TextOf(ExpressionSyntax expression, SyntaxNode? method) => expression switch
    {
        LiteralExpressionSyntax literal => literal.Token.ValueText,
        InterpolatedStringExpressionSyntax interpolated => string.Concat(interpolated.Contents.Select(part => part is InterpolatedStringTextSyntax text ? text.TextToken.ValueText : "{}")),
        IdentifierNameSyntax local => method?.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault(variable => variable.Identifier.Text == local.Identifier.Text)?.Initializer?.Value is { } value
            ? TextOf(value, method)
            : "",
        _ => "",
    };

    private static bool Removes(SyntaxNode? method, CachedTable table) =>
        method?.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(call => MethodName(call) == Removal && call.ArgumentList.ToString().Contains($"CacheTags.{table.KindConstant}", StringComparison.Ordinal)) == true;

    private static string MethodName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        _ => "",
    };

    // The member a query chain starts from: Memberships in Memberships.Where(...), TenantRoles in controlPlane.TenantRoles.
    private static string RootMember(ExpressionSyntax expression) => expression switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } => RootMember(member.Expression),
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => expression.ToString(),
    };

    private static Type? ElementOf(Type type) =>
        type.IsGenericType && type.GetGenericArguments() is [var element] && typeof(IQueryable<>).MakeGenericType(element).IsAssignableFrom(type) ? element : null;

    private static string KindConstantOf(Assembly assembly, string kind) =>
        assembly.GetTypes().Where(type => type.Name == "CacheTags")
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Where(field => field.IsLiteral))
            .Single(field => (string?)field.GetRawConstantValue() == kind).Name;

    [GeneratedRegex(@"\b(?:INSERT\s+INTO|DELETE\s+FROM|MERGE\s+INTO|TRUNCATE(?:\s+TABLE)?|UPDATE)\s+(?<table>[a-z_][a-z0-9_]*(?:\.[a-z_][a-z0-9_]*)?)(?:\s+SET\s+(?<set>.+?)(?=\bWHERE\b|\bFROM\b|\bRETURNING\b|$))?", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Writes();
}
