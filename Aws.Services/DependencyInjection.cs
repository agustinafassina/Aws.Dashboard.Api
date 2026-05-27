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
            services.AddTransient<IEc2SecurityService, Ec2SecurityService>();
            services.AddTransient<IRdsSecurityService, RdsSecurityService>();
            services.AddTransient<IIamSecurityService, IamSecurityService>();
            services.AddTransient<ICostService, CostService>();
            services.AddTransient<IInspectorSecurityService, InspectorSecurityService>();
            return services;
        }
    }
}