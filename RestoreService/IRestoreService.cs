// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

        Task ConfigureApplication(string application, List<PolicyStorageEntity> policyDetails, ClusterDetails primaryCluster, ClusterDetails secondaryCluster);

        Task ConfigureService(String applicationName, String serviceName, List<PolicyStorageEntity> policies, ClusterDetails primaryCluster, ClusterDetails secondaryCluster);

        Task<List<String>> GetConfiguredApplicationNames(String primaryClusterEndpoint, String secondaryClusterEndpoint);

        Task<string> DisconfigureApplication(string applicationName, string primaryCluster, string secondaryCluster);

        Task<string> DisconfigureService(string serviceName, string primaryCluster, string secondaryCluster);

        Task<List<Tuple<ClusterDetails, ClusterDetails>>> GetClusterCombinations();

        Task<List<PartitionWrapper>> GetStatus();

    }
}
