using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SampleAspNetCore2ApplicationNetCore.Data;
using SampleAspNetCore2ApplicationNetCore.Services;
using Kentor.AuthServices;
using Microsoft.IdentityModel.Tokens.Saml2;
using Kentor.AuthServices.Metadata;
using Kentor.AuthServices.WebSso;
using System.Security.Cryptography.X509Certificates;

namespace SampleAspNetCore2ApplicationNetCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc()
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AuthorizeFolder("/Account/Manage");
                    options.Conventions.AuthorizePage("/Account/Logout");
                });

            // Register no-op EmailSender used by account confirmation and password reset during development
            // For more information on how to enable account confirmation and password reset please visit https://go.microsoft.com/fwlink/?LinkID=532713
            services.AddSingleton<IEmailSender, EmailSender>();

            services.AddAuthentication()
                .AddSaml2(options => 
                {
                    options.SPOptions.EntityId = new Saml2NameIdentifier("https://localhost:44343/Saml2");
                    var idp = new IdentityProvider(
                        new EntityId("http://stubidp.kentor.se/Metadata"), options.SPOptions)
                        {
                            SingleSignOnServiceUrl = new Uri("http://stubidp.kentor.se/"),
                            Binding = Saml2BindingType.HttpRedirect
                        };
                    idp.SigningKeys.AddConfiguredKey(new X509Certificate2("Kentor.AuthServices.StubIdp.cer"));
                    options.IdentityProviders.Add(idp);
                });
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller}/{action=Index}/{id?}");
            });
        }
    }
}
