
# Service Fabric Application Disaster Recovery Tool
The Service Fabric Application Disaster Recovery Tool is a disaster recovery tool for Service Fabric applications which allows users to recover data from primary cluster in the event of a disaster. Service Fabric application disaster recovery tool allows the user to backup application data from their primary cluster and periodically restore it on a secondary cluster via periodic backup-restore feature.   

## Getting Started
Ensure that you have setup your Service Fabric clusters and have [backup restore service enabled](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-backuprestoreservice-quickstart-azurecluster#enabling-backup-and-restore-service) for the primary and secondary clusters. Ensure that appropriate backup policy is applied to desired application on your primary cluster so as to satisfy RPO for your disaster recovery requirements.

### Deploy Service Fabric Application Disaster Recovery Tool 
You need to deploy Service Fabric application disaster recovery tool on a Service Fabric cluster. Note that the application can be deployed on any Service Fabric cluster, it is not mandatory to deploy it on primary or secondary cluster. For deploying the application you need to first generate application package, following steps describe how to generate & deploy application package:

1. Clone this repo.
1. Update the configuration as described in [Configuration section](./README.md#configuration).
1. Build it using Visual Studio
1. Then deploy the generated application package to target Service Fabric cluster.
1. Ensure that port 8080 is opened up on the corresponding load balancer and mapped to 8080 port on backend pool.

Then after the application deployment completes successfully, open a web browser and locate to `https://<cluster url>:8080` where you can find the application landing page.

Please see the [`USAGEGUIDE`](../master/USAGEGUIDE.md) for a guide to use the tool.


## Configuration
Backup Policy credentials are encrypted using x509 certificate. The thumbprint of the certificate to be used should be specified at `PolicyStorageCertThumbprint` in `ApplicationParameters/Cloud.xml` for cloud deployements and similarly in `Local1Node.xml` / `Local5Node.xml` for local deployments. In case rollout of encryption certificate is required, then ensure that you rollout new certificate with previous certificate still installed on the machine. 

The thumbprint of the certificate to be used for HTTPS connection can be set in 'HttpsCert' `EndpointCertificate` under the `Certificates` tag in `ApplicationManifest.xml`. Also update the `GetCertificateFromStore` method in `WebService.cs` with your thumbprint.

Restore of data to application on secondary cluster is attempted periodically every 5 mins. This scans availability of new backup from primary cluster and if available then restoring it on secondary cluster. The timespan can be changed in `RestoreService.cs`, via `periodTimeSpan`.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
