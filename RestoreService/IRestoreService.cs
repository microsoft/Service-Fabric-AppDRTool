using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using PolicyStorageService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RestoreService
{
    public interface IRestoreService :  IService
    {
        Task Configure(List<String> applications, List<PolicyStorageEntity> policies, ClusterDetails primaryCluster, ClusterDetails secondaryCluster);

        Task ConfigureApplication(string application, List<PolicyStorageEntity> policyDetails, ClusterDetails primaryCluster, ClusterDetails secondaryCluster);

        Task ConfigureService(String applicationName, String serviceName, List<PolicyStorageEntity> policies, ClusterDetails primaryCluster, ClusterDetails secondaryCluster);

        Task<List<String>> GetConfiguredApplicationNames();

        Task<string> DisconfigureApplication(string applicationName);

        Task<string> DisconfigureService(string serviceName);

        Task<List<PartitionWrapper>> GetStatus();

    }
}
