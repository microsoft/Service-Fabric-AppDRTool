
# Service Fabric Application Disaster Recovery Tool
The Service Fabric Application Disaster Recovery Tool makes the application in secondary cluster data ready by periodically restoring latest backups available from primary cluster.

## Components
This application has 3 components:
 - WebInterface (stateless) : The web front-end service
 - Restore Service (stateful) : Stores partition mappings and triggers restore periodically
 - PolicyStorage Service (stateful) : Stores storage details per policy 
 
 PolicyStorage credentials are encrypted. The key can be set at `PolicyStorageEncryptionKey` in `ApplicationParameters/Cloud.xml` for cloud deployements and similarly in `Local1Node.xml` / `Local5Node.xml` for local deployments. 

## Getting Started
Clone this repo and build and deploy this application. Then after the application had started, open webbrowser and go to (localhost:8787) where you can see the application landing page.

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
