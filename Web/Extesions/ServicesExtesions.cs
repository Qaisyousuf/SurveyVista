using Data;
using Mailjet.Client;
using Microsoft.EntityFrameworkCore;
using Services.Implemnetation;
using Services.Interaces;
using System.Configuration;

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
        public static void MailConfiguration(this IServiceCollection services)
        {
            services.AddTransient<IEmailServices, EmailServices>();
        }

        //public static void MailConfiguration(this IServiceCollection services, IConfiguration configuration)
        //{
        //    services.AddControllersWithViews();

        //    services.AddScoped<IMailjetClient>(s =>
        //    {
        //        var apiKey = configuration.GetSection("Mailjet")["ApiKey"];
        //        var apiSecret = configuration.GetSection("Mailjet")["SecretKey"];
        //        return new MailjetClient(apiKey, apiSecret);
        //    });

        //}

        public static void ConfigureServicesMailJet(this IServiceCollection services, IConfiguration configuration)
        {
            // Other configurations...

            // Retrieve Mailjet settings from appsettings.json
            var mailjetSettings = configuration.GetSection("MailJet");
            var apiKey = mailjetSettings["ApiKey"];
            var apiSecret = mailjetSettings["SecretKey"];

            // Register Mailjet service with API key and secret key
            //services.AddSingleton<IMailjetService>(new MailjetService(apiKey, apiSecret));

            // Other configurations...
        }
    }
}
