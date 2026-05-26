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
            services.AddScoped<IEc2Repository, Ec2Repository>();
            services.AddScoped<IRdsRepository, RdsRepository>();
            services.AddScoped<IIamRepository, IamRepository>();
            return services;
        }
    }
}
