using Microsoft.Extensions.DependencyInjection;
using Aws.Repository.Implementations;
using Aws.Repository.Interfaces;

namespace Aws.Repository
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddSingleton<IItemRepository, ItemRepository>();
            return services;
        }
    }
}
