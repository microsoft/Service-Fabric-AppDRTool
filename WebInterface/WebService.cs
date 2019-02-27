// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
// for https
using System.Net;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace WebInterface
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class WebService : StatelessService
    {
        public WebService(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "EndpointHttps", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        return new WebHostBuilder()
                                    .UseKestrel(opt =>
                                    {
                                        int port = serviceContext.CodePackageActivationContext.GetEndpoint("EndpointHttps").Port;
                                        opt.Listen(IPAddress.IPv6Any, port, listenOptions =>
                                        {
                                            listenOptions.UseHttps(GetCertificateFromStore());
                                            listenOptions.NoDelay = true;
                                        });
                                    })
                                    .ConfigureAppConfiguration((builderContext, config) =>
                                    {
                                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                                    })
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatelessServiceContext>(serviceContext))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }

        private X509Certificate2 GetCertificateFromStore()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates;
                var currentCerts = certCollection.Find(X509FindType.FindByThumbprint, "23946BC36E25F0C99A58D4C4A76CAA6A609587D4", false);
                return currentCerts.Count == 0 ? null : currentCerts[0];
            }
            finally
            {
                store.Close();
            }
        }
    }
}