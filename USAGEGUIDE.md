
# Usage Guide for SF App Disaster Recovery Tool

How to use the Service Fabric Application Disaster Recovery Tool for your cluster backup and restore scenarios.

## Getting Started

As mentioned in the README.md, ensure you have everything set up. Locate to `localhost:8787` where you can find the application landing page.

## Connect to Service Fabric Clusters

The landing page will ask you for the details of the clusters you want to use the tool on. There is a notion of a 'Primary Cluster' and 'Secondary Cluster', which are:
 - Primary Cluster: The cluster which you would want to recover in a Disaster Recovery scenario, this is the main cluster.
 - Secondary Cluster: The cluster which periodically restores the backups from the primary on itself, this cluster acts as the restorer and can be used in the event of loss of primary as primary.

Enter these details such as:
 - Primary/Secondary Cluster endpoint: The endpoint to connect to your cluster.
 - Primary/Secondary Cluster HTTP endpoint: The HTTP endpoint to connect to your cluster.
 - Secure Thumbprint: Thumbprint of the certificate needed to connect to your secure cluster. Not needed in the case of unsecure clusters.
 - Common Name: Common Name of the subject of the certificate, needed to connect to your secure cluster. Not needed in the case of unsecure clusters.

After you've connected once to your clusters, you can select them directly when you visit the page the next time.

Click on 'Next' to follow on to the next page.

## Configure Service Fabric Cluster for Disaster Recovery

This page shows all the applications running on your primary cluster. The applications can be expanded to see the services which are part of the application. The status of the applications can be seen by their colours as follows:
 - Red: The application or service only exists on the primary cluster and not on the secondary cluster and hence cannot be configured for disaster recovery.
 - Yellow: The application or service exists on both primary and secondary and can be configured for disaster recovery.
 - Green: The application or service has been successfully configured for backup and its' backup-restore can be seen periodically on the status screen.

When you select a application or service for disaster recovery, you will be prompted to enter the credentials for the backup policies associated for that application or service. You can also edit these credentials after you've set them, for instance, if you would want to change the credentials on a rolling basis.

After you've configured all the applications or services you want for disaster recovery, click on 'Next' to follow on to the next page.

## Status of Disaster Recovery configured Applications and Services

This page shows you the status of the backup-restores of your applications and services that are taking place. If any operations fail for any reason, that specific instance will be highlighted in red for your further investigation.

The timer to check for new updates in status is configurable, and can be set to any time interval.

Note that the status page will redirect to the initial connect page if no clusters have been configured yet.