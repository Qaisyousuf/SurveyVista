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

        public static void ConfigureAddress(this IServiceCollection services)
        {
            services.AddScoped<IAddressRepository, AddressRepository>();
        }

        public static void ConfigureSocialMedia(this IServiceCollection services)
        {
            services.AddScoped<ISocialMediaRepository,SocialMediaRepository>();
        }

        public static void ConfigureFooter(this IServiceCollection services)
        {
            services.AddScoped<IFooterRepository, FooterRepository>();
        }
        public static void ConfigureQuestionnarie(this IServiceCollection services)
        {
            services.AddScoped<IQuestionnaireRepository, QuestionnaireRepository>();
        }
        public static void ConfigureQuestion(this IServiceCollection services)
        {
            services.AddScoped<IQuestionRepository, QuestionRepository>();
        }
    }
}
