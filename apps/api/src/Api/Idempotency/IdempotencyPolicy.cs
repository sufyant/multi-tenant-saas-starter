using Api.Persistence;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;
using Wolverine.Persistence.Sagas;

namespace Api.Idempotency;

internal sealed class IdempotencyPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains)
        {
            var result = chain.Handlers.SelectMany(handler => handler.Creates).Select(variable => variable.VariableType)
                .SingleOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>));
            if (result is null)
            {
                continue;
            }

            var context = chain.ServiceDependencies(container, Type.EmptyTypes)
                .Where(type => typeof(DbContext).IsAssignableFrom(type)).Distinct().SingleOrDefault() ?? typeof(ApiDbContext);
            var middleware = typeof(IdempotencyResultMiddleware<,>).MakeGenericType(result.GetGenericArguments()[0], context);
            chain.Middleware.Add(new MethodCall(middleware, "BeforeAsync"));
            chain.Postprocessors.Insert(0, new MethodCall(middleware, "AfterAsync"));
            if (!chain.IsTransactional)
            {
                rules.PersistenceProviders().Single(provider => provider.CanApply(chain, container)).ApplyTransactionSupport(chain, container);
                chain.IsTransactional = true;
            }
        }
    }
}
