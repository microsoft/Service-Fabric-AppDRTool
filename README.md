
# Service Fabric Application Disaster Recovery Tool
The Service Fabric Application Disaster Recovery Tool is a disaster recovery tool for Service Fabric clusters which allows users to recover their primary cluster data in the event of a disaster. SFAppDRTool allows the user to backup their primary cluster on a secondary cluster via periodic backup-restore on secondary cluster of your backups on primary cluster.   

## Getting Started
Ensure that you have setup your Service Fabric Clusters and have backups enabled on your primary, which is the one for which you want disaster recovery.

Clone this repo and build and deploy this application. Then after the application has started, open a web browser and locate to `localhost:8787` where you can find the application landing page.

To run this application you also need have the Service Fabric Runtime installed.

## Components
This application consists of three components:
 - WebInterface (stateless) : The web front-end service
 - Restore Service (stateful) : Stores partition mappings and triggers restore periodically
 - PolicyStorage Service (stateful) : Stores storage details per policy 


## Configuration
Backup Policy credentials are encrypted. The key can be set at `PolicyStorageEncryptionKey` in `ApplicationParameters/Cloud.xml` for cloud deployements and similarly in `Local1Node.xml` / `Local5Node.xml` for local deployments. 

Restores happen periodically every 5 mins, scanning for new backups on primary and then restoring on secondary. The timespan can be changed in `RestoreService.cs`, via `periodTimeSpan`.

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
