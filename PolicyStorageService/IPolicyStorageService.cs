// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolicyStorageService
{
    public interface IPolicyStorageService : IService
    {
        Task<bool> PostStorageDetails(List<PolicyStorageEntity> policies, string primaryClusterConnectionString, string clusterThumbprint, bool overwritePolicyDetails);

        Task<BackupStorage> GetPolicyStorageDetails(String policy);

        Task<List<String>> GetAllStoredPolicies();
    }
}
