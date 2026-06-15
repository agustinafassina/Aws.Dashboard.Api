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
            services.AddScoped<IS3Repository, S3Repository>();
            services.AddScoped<ILambdaRepository, LambdaRepository>();
            services.AddScoped<IAcmRepository, AcmRepository>();
            services.AddScoped<IEc2Repository, Ec2Repository>();
            services.AddScoped<IRdsRepository, RdsRepository>();
            services.AddScoped<IIamRepository, IamRepository>();
            services.AddScoped<ICostRepository, CostRepository>();
            services.AddScoped<IInspectorRepository, InspectorRepository>();
            services.AddScoped<IAuditRepository, AuditRepository>();
            return services;
        }
    }
}
