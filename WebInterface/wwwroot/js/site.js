// Write your JavaScript code.
var app = angular.module('SFAppDRTool', ['ngRoute', 'ui.bootstrap']);

runToast = function (text, displayClass) {
    var toast = Metro.toast.create;
    toast(text, null, 5000, displayClass);
}

app.run(function ($rootScope, $location) {
    $rootScope.selectedApps = [];
});

    // Routing to specific pages given the templates
app.config(function ($routeProvider) {
    $routeProvider

        .when('/', {
            templateUrl: 'Status',
            controller: 'SFAppDRToolController'
        })

        .when('/Configure', {
            templateUrl: 'Applications',
            controller: 'SFAppDRToolController'
        })

        .when('/servConfig', {
            templateUrl: 'ServiceConfigureModal',
            controller: 'SFAppDRToolController'
        })


        .otherwise({ redirectTo: '/' });
});


app.run(['$templateCache', function ($templateCache) {
    $templateCache.removeAll();
}]);

app.controller('SFAppDRToolController', ['$rootScope', '$scope', '$http', '$timeout', '$location', '$window', '$route', '$uibModal', function ($rootScope, $scope, $http, $timeout, $location, $window, $route, $uibModal) {

    var errorCount = 0, //Counter for the server errors
        loadPromise; //Pointer to the promise created by the Angular $timout service

    $rootScope.refreshRate = 10 * 1000; // Load the data every 10 seconds

    $scope.updateRefreshRate = function (val) {
        $rootScope.refreshRate = val * 1000;
    };

    // This will be called whenever the page is refreshed
    $scope.refresh = function () {
        $rootScope.statusLoadingFlag = true;
        $http.get('api/RestoreService/status')
            .then(function (data, status) {
                $rootScope.partitionsStatus = data.data;
                if ($rootScope.partitionsStatus == undefined) {
                    $rootScope.showConfiguredApps = false;
                    return;
                }

                console.log("Calling from status function");
                console.log($rootScope.partitionsStatus);

                $scope.transformPartitionsStatus();

                console.log("Transformed partitions status..");
                console.log($rootScope.applicationsServicesStatus);

                var appsConfigured = [];

                for (var i in $rootScope.partitionsStatus) {
                    var appName = $rootScope.partitionsStatus[i].applicationName;
                    if (appsConfigured.indexOf(appName) == -1)
                        appsConfigured.push(appName);
                }

                $rootScope.appsConfigured = appsConfigured;
                if ($rootScope.appsConfigured.length > 0)
                    $rootScope.showConfiguredApps = true;
                else 
                    $rootScope.showConfiguredApps = false;

                errorCount = 0;
                $timeout(function () {
                    $rootScope.statusLoadingFlag = false;
                }, 2000);
                nextLoad();     //Calls the next load

            }, function (data, status) {
                $rootScope.statusLoadingFlag = false;
                nextLoad(++errorCount * 2 * $rootScope.refreshRate);   // If current request fails next load will be delayed
            });
    };

    $scope.initClusterDetails = function () {

        $http.get('api/RestoreService/clustercombinations')
            .then(function (data, status) {
                $scope.clusterCombinations = data.data;
                $rootScope.clusterCombinations = $scope.clusterCombinations;
            }, function (data, status) {
                runToast("Could not load saved cluster combinations. Please try refreshing the page.", "alert");
            });
        $scope.refresh();
    }

    $scope.transformPartitionsStatus = function () {
        var partitionsStatus = $rootScope.partitionsStatus;
        var applicationToServiceNames = {};
        var applicationsStatus = {};
        for (var i = 0; i < partitionsStatus.length; i++) {
            var partitionStatus = partitionsStatus[i];
            var applicationName = partitionStatus.applicationName;
            var serviceName = partitionStatus.serviceName;
            applicationsStatus[applicationName] = applicationsStatus[applicationName] || {};
            applicationsStatus[applicationName]['data'] = applicationsStatus[applicationName]['data'] || {};
            applicationsStatus[applicationName]['status'] = applicationsStatus[applicationName]['status'] || '';
            applicationsStatus[applicationName]['data'][serviceName] = applicationsStatus[applicationName]['data'][serviceName] || {};
            applicationsStatus[applicationName]['data'][serviceName]['data'] = applicationsStatus[applicationName]['data'][serviceName]['data'] || [];
            applicationsStatus[applicationName]['data'][serviceName]['status'] = applicationsStatus[applicationName]['data'][serviceName]['status'] || '';

            applicationsStatus[applicationName]['data'][serviceName]['data'].push({
                'primaryCluster': partitionStatus.primaryCluster,
                'primaryPartitionId': partitionStatus.primaryPartitionId,
                'secondaryCluster': partitionStatus.secondaryCluster,
                'secondaryPartitionId': partitionStatus.partitionId,
                'lastBackupRestored': partitionStatus.lastBackupRestored,
                'currentlyUnderRestore': partitionStatus.currentlyUnderRestore,
                'latestBackupAvailable': partitionStatus.latestBackupAvailable,
                'restoreState': partitionStatus.restoreState
            });

            if (partitionStatus.restoreState == 'Failure') {
                applicationsStatus[applicationName]['data'][serviceName]['status'] = 'Failure';
                applicationsStatus[applicationName]['status'] = 'Failure';
            }
        }
        $rootScope.applicationsServicesStatus = applicationsStatus;
        $rootScope.applicationsServicesStatusKeys = Object.keys(applicationsStatus);

        for (var i = 0; i < $rootScope.applicationsServicesStatusKeys.length; i++) {
            var appName = $rootScope.applicationsServicesStatusKeys[i];
            var servNames = Object.keys(applicationsStatus[appName]['data']);
            applicationToServiceNames[appName] = servNames;
        }

        $rootScope.applicationToServiceNames = applicationToServiceNames;
    }

    var cancelNextLoad = function () {
        $timeout.cancel(loadPromise);
    };

    var nextLoad = function (mill) {
        var loadTime = $rootScope.refreshRate;
        mill = mill || loadTime;

        //Always make sure the last timeout is cleared before starting a new one
        cancelNextLoad();
        loadPromise = $timeout($scope.refresh, mill);
    };

    //Always clear the timeout when the view is destroyed, otherwise it will keep polling and leak memory
    $scope.$on('$destroy', function () {
        cancelNextLoad();
    });

    $scope.configure = function () {

        var contentData = {};
        contentData.PoliciesList = $scope.policies;
        contentData.ApplicationsList = $rootScope.selectedApps;

        var content = JSON.stringify(contentData);

        $http.post('api/RestoreService/configure/' + $rootScope.pc + '/' + $rootScope.sc + '/' + $rootScope.php + '/' + $rootScope.shp, content)
            .then(function (data, status) {
                window.alert("Applications Successfully configured");
            }, function (data, status) {
                window.alert("Applications not configured. Try again");
            });
        $scope.cancel(true);
    };

    $scope.configureApplication = function () {

        var applicationName = $rootScope.currentAppname;
        console.log("Calling from configure application");
        console.log(applicationName);

        var contentData = {};
        contentData.PoliciesList = $scope.apppolicies;
        // TODO validate policies and update the configured services in UI by making them green
        contentData.ApplicationList = [applicationName]; // Make sure this fabric:/applicationName

        console.log("Calling from configureApplication");
        console.log(contentData);
        var content = JSON.stringify(contentData);

        if (!$scope.validatePolicies(contentData)) {
            runToast("Could not configure application for DR. Please ensure that you enter the credentials for all policies associated with the application. Please try again.", "alert");
            return;
        }

        $http.post('api/RestoreService/configureapp/' + $rootScope.primaryClusterEndpoint + '/' + $rootScope.primaryClusterHTTPEndpoint.replace("//", "__") + '/' + $rootScope.primaryClusterThumbprint + '/' + $rootScope.primaryClusterCommonName + '/'
            + $rootScope.secondaryClusterEndpoint + '/' + $rootScope.secondaryClusterHTTPEndpoint.replace("//", "__") + '/' + $rootScope.secondaryClusterThumbprint + '/' + $rootScope.secondaryClusterCommonName, content)
            .then(function (data, status) {
                console.log("Calling success function");
                console.log($scope.appsData);
                $scope.appsStatusData[applicationName] = "Configured"; 
                for (var i = 0; i < $scope.appsData[applicationName].length; i++) {
                    if ($scope.appsData[applicationName][i][1] == "NotConfigured") {
                        $scope.appsData[applicationName][i][1] = "Configured";
                    }
                }
                runToast("Applications successfully configured", "success");
            }, function (data, status) {
                runToast("Applications not configured. Try again", "alert");
            });
    }

    $scope.validatePolicies = function (contentData) {
        policiesList = contentData.PoliciesList;

        for (var i = 0; i < policiesList.length; i++) {
            var policy = policiesList[i];
            if (policy.backupStorage.storageKind == 'AzureBlobStore') {
                var cs = policy.backupStorage.connectionString;
                if (cs == "" || cs == "****") {
                    return false;
                }
            }
            else {
                var pu = policy.backupStorage.primaryUsername;
                var pp = policy.backupStorage.primaryPassword;
                var su = policy.backupStorage.secondaryUsername;
                var sp = policy.backupStorage.secondaryPassword;
                if (pu == "" || pp == "" || su == "" || sp == "") {
                    return false;
                }
            }
        }

        return true;
    }

    $scope.disconfigureApplication = function () {
        var applicationName = $rootScope.currentAppname;

        var contentData = {};
        contentData.ApplicationList = [applicationName];

        var content = JSON.stringify(contentData);

        var primaryClusterName = $rootScope.primaryClusterEndpoint.split(':')[0];
        var secondaryClusterName = $rootScope.secondaryClusterEndpoint.split(':')[0];

        $http.post('api/RestoreService/disconfigureapp/' + primaryClusterName + '/' + secondaryClusterName, content)
            .then(function (data, status) {
                // Disconfigure successful
                $scope.appsStatusData[applicationName] = "NotConfigured";
                for (var i = 0; i < $scope.appsData[applicationName].length; i++) {
                    if ($scope.appsData[applicationName][i][1] == "Configured") {
                        $scope.appsData[applicationName][i][1] = "NotConfigured";
                    }
                }
                runToast("Application successfuly disabled for backup.", "success");
            }, function (data, status) {
                // Disconfigure unsuccessful
                runToast("Application could not be disabled for backup. Please try again.", "alert");
            });
    }

    $scope.configureService = function () {

        var serviceName = $rootScope.currentServicename;
        console.log("Calling from configure service");
        console.log(serviceName);

        var names = serviceName.split('/');
        names.pop();
        var appName = names.join('/');
        console.log("App name is " + appName);
        $rootScope.appNameServ = appName;

        var contentData = {};
        contentData.PoliciesList = $scope.policies;
        // TODO validate policies and update the configured services in UI by making them green
        contentData.ServiceList = [appName, serviceName]; // Make sure this fabric:/applicationName/serviceName

        console.log("Calling from configureApplication");
        console.log(contentData);
        var content = JSON.stringify(contentData);

        if (!$scope.validatePolicies(contentData)) {
            runToast("Could not configure service for DR. Please ensure that the credentials for policy associated with service is not empty. Please try again.", "alert");
            return;
        }

        $http.post('api/RestoreService/configureservice/' + $rootScope.primaryClusterEndpoint + '/' + $rootScope.primaryClusterHTTPEndpoint.replace("//", "__") + '/' + $rootScope.primaryClusterThumbprint + '/' + $rootScope.primaryClusterCommonName + '/'
            + $rootScope.secondaryClusterEndpoint.replace("//", "__") + '/' + $rootScope.secondaryClusterHTTPEndpoint.replace("//", "__") + '/' + $rootScope.secondaryClusterThumbprint + '/' + $rootScope.secondaryClusterCommonName, content)
            .then(function (data, status) {
                for (var i = 0; i < $scope.appsData[$rootScope.appNameServ].length; i++) {
                    if ($scope.appsData[$rootScope.appNameServ][i][0] == $rootScope.currentServicename) {
                        $scope.appsData[$rootScope.appNameServ][i][1] = "Configured";
                    }
                }
                runToast("Service successfully configured.", "success");
            }, function (data, status) {
                runToast("Service not configured. Please try again.", "alert");
            });
    }

    $scope.disconfigureService = function () {
        var serviceName = $rootScope.currentServicename;

        var names = serviceName.split('/');
        names.pop();
        var appName = names.join('/');
        console.log("App name is " + appName);
        $rootScope.appNameServ = appName;

        var contentData = {};
        contentData.ServiceList = [serviceName];

        var content = JSON.stringify(contentData);

        var primaryClusterName = $rootScope.primaryClusterEndpoint.split(':')[0];
        var secondaryClusterName = $rootScope.secondaryClusterEndpoint.split(':')[0];

        $http.post('api/RestoreService/disconfigureservice/' + primaryClusterName + '/' + secondaryClusterName, content)
            .then(function (data, status) {
                // Disconfigure successful
                for (var i = 0; i < $scope.appsData[$rootScope.appNameServ].length; i++) {
                    if ($scope.appsData[$rootScope.appNameServ][i][0] == $rootScope.currentServicename) {
                        $scope.appsData[$rootScope.appNameServ][i][1] = "NotConfigured";
                    }
                }
                runToast("Service successfuly disabled for backup.", "success");
            }, function (data, status) {
                // Disconfigure unsuccessful
                runToast("Service could not be disabled for backup. Please try again.", "alert");
            });
    }


    $scope.status = {
    isFirstOpen: true,
    isFirstDisabled: false
    };

    $scope.editPolicyOfApp = function (policyName) {
        console.log("In editPolicyOfApp");
        $rootScope.currentPolicyUnderEdit = policyName;
        $rootScope.appPolicyEditFlag = true;
        var index = $rootScope.storedPolicies.indexOf(policyName);
        if (index > -1) {
            $rootScope.storedPolicies.splice(index, 1);
        }
    }

    $scope.saveEditPolicyOfApp = function () {
        console.log("In saveEditPolicyOfApp");
        var policyName = $rootScope.currentPolicyUnderEdit;
        var contentData = {};
        var policyInd = -1;

        for (var i = 0; i < $scope.apppolicies.length; i++) {
            if ($scope.apppolicies[i].policy == policyName) {
                policyInd = i;
                break;
            }
        }

        contentData.PoliciesList = [$scope.apppolicies[policyInd]];

        var content = JSON.stringify(contentData);

        var clusterEndp = $rootScope.primaryClusterHTTPEndpoint;
        clusterEndp = clusterEndp.replace("//", "__");

        $http.post('api/RestoreService/updatepolicy/' + clusterEndp + '/' + $rootScope.primaryClusterThumbprint, content)
            .then(function (data, status) {
                $scope.getStoredPolicies();
                $rootScope.appPolicyEditFlag = false;
                runToast("Policy successfully edited", "success");
            }, function (data, status) {
                $rootScope.storedPolicies.push($rootScope.currentPolicyUnderEdit);
                $rootScope.appPolicyEditFlag = false;
                runToast("Policy could not be edited. Please try again", "alert");
            });
    }

    $scope.cancelEditPolicyOfApp = function () {
        $rootScope.storedPolicies.push($rootScope.currentPolicyUnderEdit);
        $rootScope.appPolicyEditFlag = false;
    }

    $scope.editPolicyOfService = function (policyName) {
        $rootScope.currentPolicyUnderEdit = policyName;
        $rootScope.servicePolicyEditFlag = true;
        var index = $rootScope.storedPolicies.indexOf(policyName);
        if (index > -1) {
            $rootScope.storedPolicies.splice(index, 1);
        }
    }

    $scope.saveEditPolicyOfService = function () {
        var policyName = $rootScope.currentPolicyUnderEdit;
        var contentData = {};
        var policyInd = -1;

        for (var i = 0; i < $scope.policies.length; i++) {
            if ($scope.policies[i].policy == policyName) {
                policyInd = i;
                break;
            }
        }

        contentData.PoliciesList = [$scope.policies[policyInd]];

        var content = JSON.stringify(contentData);

        var clusterEndp = $rootScope.primaryClusterHTTPEndpoint;
        clusterEndp = clusterEndp.replace("//", "__");

        $http.post('api/RestoreService/updatepolicy/' + clusterEndp + '/' + $rootScope.primaryClusterThumbprint, content)
            .then(function (data, status) {
                $scope.getStoredPolicies();
                $rootScope.servicePolicyEditFlag = false;
                runToast("Policy successfully edited", "success");
            }, function (data, status) {
                $rootScope.storedPolicies.push($rootScope.currentPolicyUnderEdit);
                $rootScope.servicePolicyEditFlag = false;
                runToast("Policy could not be edited. Please try again", "alert");
            });
    }

    $scope.cancelEditPolicyOfService = function () {
        $rootScope.storedPolicies.push($rootScope.currentPolicyUnderEdit);
        $rootScope.servicePolicyEditFlag = false;
    }

    $scope.openServicePolicyModal = function (serviceName, serviceStatus) {
        $scope.getStoredPolicies();
        $rootScope.currentServicename = serviceName;
        $rootScope.serviceDisableFlag = false;
        $rootScope.servicePolicyEditFlag = false;

        if (serviceStatus == 'Configured') {
            $rootScope.serviceDisableFlag = true;
        }

        serviceN = serviceName.replace("fabric:/", "");
        serviceN = serviceN.replace("/", "_");
        console.log("The service name is " + serviceN);
        clusterEndp = $rootScope.primaryClusterHTTPEndpoint;
        clusterEndp = clusterEndp.replace("//", "__");
        clusterThumbprint = $rootScope.primaryClusterThumbprint;
        console.log("The cluster name is " + clusterEndp);
        Metro.dialog.open('#policyConfigModal');
        $rootScope.serviceConfigLoad = true;
        $rootScope.serviceNoPolicyFoundFlag = false;
        $scope.policies = undefined;
        $rootScope.policies = undefined;
        $http.get('api/RestoreService/servicepolicies/' + clusterEndp + '/' + clusterThumbprint + '/' + serviceN)
            .then(function (data, status) {
                $scope.policies = data.data;
                console.log($scope.policies);
                $rootScope.policies = $scope.policies;
                $rootScope.serviceConfigLoad = false;
            }, function (data, status) {
                $scope.policies = undefined;
                $rootScope.serviceNoPolicyFoundFlag = true;
                $rootScope.serviceConfigLoad = false;
            });
    }

    $scope.openAppPolicyModalWrapper = function (appName, appStatus) {
        $scope.storedPolicies = undefined;
        $rootScope.storedPolicies = undefined;
        $http.get('api/RestoreService/storedpolicies')
            .then(function (data, status) {
                $scope.storedPolicies = data.data;
                $rootScope.storedPolicies = $scope.storedPolicies;
                console.log("Stored policies are..");
                console.log($rootScope.storedPolicies);

                $scope.openAppPolicyModal(appName, appStatus);

            }, function (data, status) {
                $scope.storedPolicies = undefined;
                runToast('Could not load all stored policies. Please try again.', 'alert');
            });
    }

    $scope.openAppPolicyModal = function (appName, appStatus) {
        $scope.getStoredPolicies();
        console.log("Inside openAppPolicyModal..");
        console.log("openAppPolicyModal storedPolicies are");
        console.log($rootScope.storedPolicies);
        console.log("end openAppPolicyModal");

        $rootScope.currentAppname = appName;
        $rootScope.appPolicyEditFlag = false;
        $rootScope.appDisableFlag = false;

        if (appStatus == 'Configured') {
            $rootScope.appDisableFlag = true;
        }

        appNameN = appName.replace("fabric:/", "");
        clusterEndp = $rootScope.primaryClusterHTTPEndpoint;
        clusterEndp = clusterEndp.replace("//", "__");
        clusterThumbprint = $rootScope.primaryClusterThumbprint;
        Metro.dialog.open('#appPolicyConfigModal');
        $rootScope.appConfigLoad = true;
        $rootScope.appNoPolicyFoundFlag = false;
        $scope.apppolicies = undefined;
        $rootScope.apppolicies = undefined;
        $http.get('api/RestoreService/apppolicies/' + clusterEndp + '/' + clusterThumbprint + '/' + appNameN)
            .then(function (data, status) {
                console.log("In function openAppPolicyModal");
                $scope.apppolicies = data.data;
                console.log($scope.apppolicies);
                $rootScope.apppolicies = $scope.apppolicies;
                $rootScope.appConfigLoad = false;
            }, function (data, status) {
                runToast("Could not load application policies. Please try again.", "alert");
                $scope.apppolicies = undefined;
                $rootScope.appConfigLoad = false;
                $rootScope.appNoPolicyFoundFlag = true;
            });
    }

    $scope.getServiceConfigModal = function () {
        console.log("getServiceConfigModal was called");
        $location.path("/servConfig");
    }

    $scope.gotoindex = function () {
        $location.path("/&rt=" + Math.random());
    }

    $scope.gotoconfigure = function () {
        $window.location.href = '#!Configure';
        $window.location.reload();
    }

    $scope.gotoconfigfromstatus = function () {
        runToast("No Applications configured yet for disaster recovery. Redirecting to Configure page.", "info");
        $scope.gotoconfigure();
    }

    $scope.getAppsOnPrimaryCluster = function () {
        
        $scope.appsKeys = undefined;
        $scope.appsData = undefined;
        $scope.policies = undefined;
        $scope.apppolicies = undefined;
        $rootScope.policies = undefined;
        $rootScope.apppolicies = undefined;
        $scope.storedPolicies = undefined;
        $rootScope.storedPolicies = undefined;
        $rootScope.appsKeys = undefined;
        $rootScope.appsData = undefined;


        $rootScope.primaryClusterEndpoint = $scope.primaryClusterEndpoint;
        $rootScope.secondaryClusterEndpoint = $scope.secondaryClusterEndpoint;

        $rootScope.primaryClusterHTTPEndpoint = $scope.primaryClusterHTTPEndpoint;
        $rootScope.secondaryClusterHTTPEndpoint = $scope.secondaryClusterHTTPEndpoint;

        $rootScope.primaryClusterThumbprint = $scope.primSecureThumbp;
        $rootScope.secondaryClusterThumbprint = $scope.secSecureThumbp;

        $rootScope.primaryClusterCommonName = $scope.primaryCommonName;
        $rootScope.secondaryClusterCommonName = $scope.secondaryCommonName;

        if (!$scope.validateClusterDetails()) {
            runToast("Please ensure that you have entered all the details to connect to clusters. Try again.", "alert");
            return;
        }

        $location.path("/servConfig").search('rt', Math.random());
        $rootScope.splashLoad = true;

        $http.get('api/RestoreService/apps/' + $scope.primaryClusterEndpoint + '/' + $scope.primSecureThumbp + '/' + $scope.primaryCommonName + '/' + $scope.secondaryClusterEndpoint + '/' + $scope.secSecureThumbp + '/' + $scope.secondaryCommonName)
            .then(function (data, status) {
                $rootScope.splashLoad = false;
                $scope.apps = data;

                var appStatusData = {};
                var datainit = data.data;
                for (var key in datainit) {
                    var appStatus = datainit[key].shift();
                    appStatusData[key] = appStatus[1];
                }

                $scope.appsStatusData = appStatusData;
                $scope.appsData = datainit;
                $scope.appsKeys = Object.keys($scope.appsData);
                $rootScope.appsData = $scope.appsData;
                $rootScope.appsKeys = $scope.appsKeys;
                $rootScope.appsStatusData = $scope.appsStatusData;
                console.log($scope.appsStatusData);
                console.log($scope.appsData);
                console.log($scope.appsKeys);
                $scope.getStoredPolicies();
            }, function (data, status) {
                $scope.apps = undefined;
                $rootScope.splashLoad = false;
                runToast('Please check the cluster details and try again', 'alert');
            });
    };

    $scope.validateClusterDetails = function () {
        if (!$rootScope.primaryClusterEndpoint ||
            !$rootScope.primaryClusterEndpoint.includes(":") ||
            !$rootScope.primaryClusterHTTPEndpoint.includes("http") ||
            $rootScope.primaryClusterThumbprint == "" ||
            $rootScope.primaryClusterCommonName == "" ||
            !$rootScope.secondaryClusterEndpoint ||
            !$rootScope.primaryClusterEndpoint.includes(":") ||
            !$rootScope.secondaryClusterHTTPEndpoint.includes("http") ||
            $rootScope.secondaryClusterThumbprint == "" ||
            $rootScope.secondaryClusterCommonName == "") {
            return false;
        }
        return true;     
    }

    $scope.updateHTTPEndpoint = function (cluster) {
        if (cluster == 'primary') {
            var tcpe = $scope.primaryClusterEndpoint;
            var tcparr = tcpe.split(':');
            var tcpPort;
            if (tcparr.length == 1) {
                tcpPort = "19000"
            }
            else {
                tcpPort = tcparr[1]; // should usually be 19000
            }

            var httpe = tcpe.replace(tcpPort, "19080");
            httpe = "https://" + httpe;
            $scope.primaryClusterHTTPEndpoint = httpe;
        }
        else {
            var tcpe = $scope.secondaryClusterEndpoint;
            var tcparr = tcpe.split(':');
            var tcpPort;
            if (tcparr.length == 1) {
                tcpPort = "19000"
            }
            else {
                tcpPort = tcparr[1]; // should usually be 19000
            }
            
            var httpe = tcpe.replace(tcpPort, "19080");
            httpe = "https://" + httpe;
            $scope.secondaryClusterHTTPEndpoint = httpe;
        }
    }


    $scope.getAppsOnSelectedCluster = function (clusterCombination) {
        var primaryCluster = clusterCombination.item1;
        var secondaryCluster = clusterCombination.item2;
        $scope.primaryClusterEndpoint = primaryCluster.clientConnectionEndpoint;
        $scope.primaryClusterHTTPEndpoint = primaryCluster.httpEndpoint;
        $scope.secondaryClusterEndpoint = secondaryCluster.clientConnectionEndpoint;
        $scope.secondaryClusterHTTPEndpoint = secondaryCluster.httpEndpoint;
        $scope.primSecureThumbp = primaryCluster.certificateThumbprint;
        $scope.secSecureThumbp = secondaryCluster.certificateThumbprint;
        $scope.primaryCommonName = primaryCluster.commonName;
        $scope.secondaryCommonName = secondaryCluster.commonName;
        $scope.getAppsOnPrimaryCluster();
        //angular.element('#clusterConfigButton').triggerHandler('click');
    }

    $scope.getStoredPolicies = function () {
        $scope.storedPolicies = undefined;
        $rootScope.storedPolicies = undefined;
        $http.get('api/RestoreService/storedpolicies')
            .then(function (data, status) {
                $scope.storedPolicies = data.data;
                $rootScope.storedPolicies = $scope.storedPolicies;
                console.log("Stored policies are..");
                console.log($rootScope.storedPolicies);
            }, function (data, status) {
                $scope.storedPolicies = undefined;
                runToast('Could not load all stored policies. Please try again.', 'alert');
            });
    }

}]);