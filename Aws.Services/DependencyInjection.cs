using Microsoft.Extensions.DependencyInjection;
using Aws.Services.Implementations;
using Aws.Services.Interfaces;
using Aws.Services.Mappers;

namespace Aws.Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<ItemMapper>();
            services.AddTransient<IItemService, ItemService>();
            services.AddTransient<IS3SecurityService, S3SecurityService>();
            return services;
        }
    }
}
