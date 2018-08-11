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
        Task<bool> PostStorageDetails(List<PolicyStorageEntity> policies, string primaryClusterConnectionString);

        Task<BackupStorage> GetPolicyStorageDetails(String policy);

        Task<List<String>> GetAllStoredPolicies();
    }
}
