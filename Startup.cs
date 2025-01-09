using GoolsDev.App.Cosmos;
using GoolsDev.App.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace GoolsDev.App
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddOptions<BmgcFantasyPlayoffsSettings>()
                .BindConfiguration("BmgcFantasyPlayoffsSettings");
            services
                .AddAuthentication()
                .AddGoogle(options =>
                {
                    IConfigurationSection googleAuthNSection =
                        Configuration.GetSection("Authentication:Google");

                    options.ClientId = googleAuthNSection["ClientId"];
                    options.ClientSecret = googleAuthNSection["ClientSecret"];
                });

            services.AddSingleton<ICosmosGames, CosmosGames>()
                .AddSingleton<ICosmosPlayerStats, CosmosPlayerStats>()
                .AddSingleton(s =>
            {
                var section = Configuration.GetSection("CosmosSettings");
                var endpoint = section["DbEndPoint"];
                var key = section["DBKey"];
                return new CosmosClient(endpoint, key,
                new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)

        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection()
                .UseStaticFiles()
                .UseRouting()
                .UseAuthorization()
                .UseResponseCaching()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute(
                        name: "default",
                        pattern: "{controller=Home}/{action=Index}/{id?}");
                });
        }
    }
}