﻿using System;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using Common.Log;
using Lykke.Common.ApiLibrary.Middleware;
using Lykke.Common.ApiLibrary.Swagger;
using Lykke.Common.Log;
using Lykke.Logs;
using Lykke.Service.PayAuth.Core.Services;
using Lykke.Service.PayAuth.Core.Settings;
using Lykke.Service.PayAuth.Modules;
using Lykke.SettingsReader;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Service.PayAuth
{
    public class Startup
    {
        public IHostingEnvironment Environment { get; }
        public IContainer ApplicationContainer { get; private set; }
        public IConfigurationRoot Configuration { get; }
        public ILog Log { get; private set; }
        
        private IHealthNotifier HealthNotifier { get; set; }

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddEnvironmentVariables();
            Configuration = builder.Build();

            Environment = env;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            try
            {
                services.AddMvc()
                    .AddJsonOptions(options =>
                    {
                        options.SerializerSettings.ContractResolver =
                            new Newtonsoft.Json.Serialization.DefaultContractResolver();
                    });

                services.AddSwaggerGen(options =>
                {
                    options.DefaultLykkeConfiguration("v1", "PayAuth API");
                });

                Mapper.Initialize(cfg =>
                {
                    cfg.AddProfiles(GetType().Assembly);
                    cfg.AddProfiles(typeof(Services.AutoMapperProfile));
                });
                
                Mapper.Configuration.AssertConfigurationIsValid();

                var builder = new ContainerBuilder();

                var appSettings = Configuration.LoadSettings<AppSettings>(opt => { });

                services.AddLykkeLogging(
                    appSettings.ConnectionString(x => x.PayAuthService.Db.LogsConnString),
                    "PayAuthLog",
                    appSettings.CurrentValue.SlackNotifications.AzureQueue.ConnectionString,
                    appSettings.CurrentValue.SlackNotifications.AzureQueue.QueueName);

                builder.RegisterModule(new ServiceModule(appSettings.Nested(x => x.PayAuthService)));

                builder.RegisterModule(new CqrsModule(
                    appSettings.Nested(x => x.PayAuthService.Cqrs), 
                    appSettings.CurrentValue.PayAuthService.ResetPasswordUrlTemplate));

                builder.Populate(services);

                ApplicationContainer = builder.Build();

                Log = ApplicationContainer.Resolve<ILogFactory>().CreateLog(this);

                HealthNotifier = ApplicationContainer.Resolve<IHealthNotifier>();

                return new AutofacServiceProvider(ApplicationContainer);
            }
            catch (Exception ex)
            {
                Log?.Critical(ex);
                throw;
            }
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            try
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseLykkeMiddleware(ex => new { Message = "Technical problem" });

                app.UseMvc();

                app.UseSwagger(c =>
                {
                    c.PreSerializeFilters.Add((swagger, httpReq) => swagger.Host = httpReq.Host.Value);
                });

                app.UseSwaggerUI(x =>
                {
                    x.RoutePrefix = "swagger/ui";
                    x.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                });

                app.UseStaticFiles();

                appLifetime.ApplicationStarted.Register(() => StartApplication().GetAwaiter().GetResult());
                appLifetime.ApplicationStopping.Register(() => StopApplication().GetAwaiter().GetResult());
                appLifetime.ApplicationStopped.Register(CleanUp);
            }
            catch (Exception ex)
            {
                Log?.Critical(ex);
                throw;
            }
        }

        private async Task StartApplication()
        {
            try
            {
                // NOTE: Service not yet recieve and process requests here

                await ApplicationContainer.Resolve<IStartupManager>().StartAsync();

                HealthNotifier.Notify("Started");
            }
            catch (Exception ex)
            {
                Log?.Critical( ex);
                throw;
            }
        }

        private async Task StopApplication()
        {
            try
            {
                // NOTE: Service still can recieve and process requests here, so take care about it if you add logic here.

                await ApplicationContainer.Resolve<IShutdownManager>().StopAsync();
            }
            catch (Exception ex)
            {
                Log?.Critical(ex);
                throw;
            }
        }

        private void CleanUp()
        {
            try
            {
                HealthNotifier?.Notify("Terminating");

                ApplicationContainer.Dispose();
            }
            catch (Exception ex)
            {
                Log?.Critical(ex);
                (Log as IDisposable)?.Dispose();
                throw;
            }
        }
    }
}
