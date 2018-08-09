using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WebInterface
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
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                /*routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Applications}/{id?}");*/

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "Status",
                    template: "Status",
                    defaults: new { controller = "Home", action = "Status" });

                routes.MapRoute(
                   name: "policies",
                   template: "Policies",
                   defaults: new { controller = "Home", action = "Policies" });

                routes.MapRoute(
                   name: "applications",
                   template: "Applications",
                   defaults: new { controller = "Home", action = "Applications" });

                routes.MapRoute(
                    name: "HomePage",
                    template: "HomePage",
                    defaults: new { controller = "Home", action = "Home" });

                routes.MapRoute(
                    name: "PolicyModal",
                    template: "PolicyModal",
                    defaults: new { controller = "Home", action = "PolicyModal" });

                routes.MapRoute(
                    name: "Modal",
                    template: "Modal",
                    defaults: new { controller = "Home", action = "Modal" });

                routes.MapRoute(
                    name: "ConfigureModal",
                    template: "ConfigureModal",
                    defaults: new { controller = "Home", action = "ConfigureModal" });

                routes.MapRoute(
                    name: "StatusModal",
                    template: "StatusModal",
                    defaults: new { controller = "Home", action = "StatusModal" });

                routes.MapRoute(
                    name: "ServiceConfigureModal",
                    template: "ServiceConfigureModal",
                    defaults: new { controller = "Home", action = "ServiceConfigureModal" });
            });
        }
    }
}
