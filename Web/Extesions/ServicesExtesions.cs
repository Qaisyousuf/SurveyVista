using Data;
using Microsoft.EntityFrameworkCore;
using Services.Implemnetation;
using Services.Interaces;

namespace Web.Extesions
{
    public static class ServicesExtesions
    {
        public static void ConfigureSQLConnection(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<SurveyContext>(option =>
            {
                option.UseSqlServer(configuration.GetConnectionString("SurveyVista"), cfg => cfg.MigrationsAssembly("Web"));
            });
        }

        public static void ConfigurePageServices(this IServiceCollection services)
        {
            services.AddScoped<IPageRepository,PageRepository>();
        }
        public static void ConfigureBannerServices(this IServiceCollection services)
        {
            services.AddScoped<IBannerRepository, BannerRepository>();
        }
    }
}
