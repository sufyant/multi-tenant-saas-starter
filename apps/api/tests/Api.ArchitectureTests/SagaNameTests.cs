using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Api.ArchitectureTests;

// N2: a saga says what it is. Its class name ends with Saga and its table name ends with _sagas, so a reader tells the saga record
// from the business data it drives.
public sealed class SagaNameTests
{
    [Fact]
    public void NameSagas_EverySagaClass_EndsWithSaga()
    {
        var sagas = Solution.LayerOfEveryModule("Application")
            .SelectMany(project => Solution.Load(project).GetTypes())
            .Where(type => type.IsAssignableTo(typeof(Saga)) && !type.IsAbstract)
            .ToList();

        var misnamed = sagas.Where(saga => !saga.Name.EndsWith("Saga", StringComparison.Ordinal)).Select(saga => saga.FullName);

        sagas.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    [Fact]
    public void NameSagaTables_EverySagaTable_EndsWithSagas()
    {
        var tables = ModuleDbContexts.Types()
            .SelectMany(SagaTablesOf)
            .ToList();

        var misnamed = tables.Where(table => !table.EndsWith("_sagas", StringComparison.Ordinal));

        tables.ShouldNotBeEmpty();
        misnamed.ShouldBeEmpty();
    }

    private static IEnumerable<string> SagaTablesOf(Type contextType)
    {
        using var context = ModuleDbContexts.CreateAsDotnetEfDoes(contextType);

        return
        [
            .. context.Model.GetEntityTypes()
                .Where(entity => entity.ClrType.IsAssignableTo(typeof(Saga)))
                .Select(entity => $"{entity.GetSchema()}.{entity.GetTableName()}"),
        ];
    }
}
