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

app.controller('SFAppDRToolController', ['$rootScope', '$scope', '$http', '$timeout', '$location','$uibModal', function ($rootScope, $scope, $http, $timeout, $location, $uibModal) {

    var loadTime = 10000, //Load the data every second
        errorCount = 0, //Counter for the server errors
        loadPromise; //Pointer to the promise created by the Angular $timout service


    // This will be called whenever the page is refreshed
    $scope.refresh = function () {
        $http.get('api/RestoreService/status')
            .then(function (data, status) {
                $rootScope.partitionsStatus = data.data;
                if ($rootScope.partitionsStatus == undefined) {
                    $rootScope.showConfiguredApps = false;
                    return;
                }

                console.log("Calling from status function");
                console.log($rootScope.partitionsStatus);

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
                nextLoad();     //Calls the next load

            }, function (data, status) {
                nextLoad(++errorCount * 2 * loadTime);   // If current request fails next load will be delayed
            });
    };

    var cancelNextLoad = function () {
        $timeout.cancel(loadPromise);
    };

    var nextLoad = function (mill) {
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

        $http.post('api/RestoreService/configureapp/' + $rootScope.primaryClusterEndpoint + '/' + $rootScope.primaryClusterThumbprint + '/' + $rootScope.primaryClusterCommonName + '/' + $rootScope.secondaryClusterEndpoint + '/' + $rootScope.secondaryClusterThumbprint + '/' + $rootScope.secondaryClusterCommonName, content)
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

    $scope.disconfigureApplication = function () {
        var applicationName = $rootScope.currentAppname;

        var contentData = {};
        contentData.ApplicationList = [applicationName];

        $http.post('api/RestoreService/disconfigureapp/', content)
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

        $http.post('api/RestoreService/configureservice/' + $rootScope.primaryClusterEndpoint + '/' + $rootScope.primaryClusterThumbprint + '/' + $rootScope.primaryClusterCommonName + '/' + $rootScope.secondaryClusterEndpoint + '/' + $rootScope.secondaryClusterThumbprint + '/' + $rootScope.secondaryClusterCommonName, content)
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

        var contentData = {};
        contentData.ServiceList = [serviceName];

        $http.post('api/RestoreService/disconfigureservice/', content)
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

   
    $scope.cancel = function (modalInstance) {
        if (modalInstance === 'configureModalInstance')
            $scope.configureModalInstance.dismiss();

        else if (modalInstance === 'policyModalInstance')
            $scope.policyModalInstance.dismiss();

        else if (modalInstance === 'statusModalInstance')
            $scope.statusModalInstance.dismiss();

        else {
            $scope.configureModalInstance.dismiss();
            $scope.policyModalInstance.dismiss();
        }
    };

    $scope.status = {
    isFirstOpen: true,
    isFirstDisabled: false
    };

    $scope.openStatusModal = function (configuredApp) {
        $scope.configuredApp = configuredApp;
        $scope.applicationStatus = [];
        for (var i in $rootScope.partitionsStatus) {
            if ($scope.partitionsStatus[i].applicationName.includes(configuredApp)) {
                $scope.applicationStatus.push($rootScope.partitionsStatus[i]);
            }
        }
        $scope.statusModalInstance = $uibModal.open({
            templateUrl: 'StatusModal',
            scope: $scope,
            windowClass: 'app-modal-window'
        });
    };

    $scope.disconfigure = function (configuredApp) {
        if (configuredApp.includes("fabric:/"))
            configuredApp = configuredApp.replace('fabric:/', '');
        $http.get('api/RestoreService/disconfigure/' + configuredApp)
            .then(function (data, status) {
                if (data.data == configuredApp)
                    window.alert("Suuccessfully disconfigured");
                $scope.cancel('statusModalInstance');
            }, function (data, status) {
                window.alert("Problem while disconfiguring");
            });
    }

    $scope.toggleSelection = function (app) {
        var idx = $rootScope.selectedApps.indexOf(app);

        // Is currently selected
        if (idx > -1) {
            $rootScope.selectedApps.splice(idx, 1);
        }

        // Is newly selected
        else {
            $rootScope.selectedApps.push(app);
        }
    };

    $scope.getapps = function () {

        $rootScope.pc = $scope.pc;
        $rootScope.sc = $scope.sc;
        $rootScope.php = $scope.php;
        $rootScope.shp = $scope.shp;

        var primaryAddress = $scope.pc;

        if (primaryAddress.includes("http://"))
            primaryAddress = primaryAddress.replace("http://", "");

        if (primaryAddress.includes("https://"))
            primaryAddress = primaryAddress.replace("https://", "");

        $scope.pc = $rootScope.pc = primaryAddress;

        var secondaryAddress = $scope.sc;

        if (secondaryAddress.includes("http://"))
            secondaryAddress = secondaryAddress.replace("http://", "");

        if (secondaryAddress.includes("https://"))
            secondaryAddress = secondaryAddress.replace("https://", "");

        $scope.sc = $rootScope.sc = secondaryAddress;

        $http.get('api/RestoreService/' + $scope.pc + '/' + $scope.php)
            .then(function (data, status) {
                $scope.apps = data;
                $scope.configureModalInstance = $uibModal.open({
                    templateUrl: 'ConfigureModal',
                    scope: $scope,
                    windowClass: 'app--window'
                });
            }, function (data, status) {
                $scope.apps = undefined;
                window.alert('Please check the cluster details and try again');
            });
    };

    $scope.openPolicyModal = function () {
        $http.post('api/RestoreService/policies/' + $scope.pc + ':' + $scope.php, $rootScope.selectedApps)
            .then(function (data, status) {
                $scope.policies = data.data;
                console.log($scope.policies[0].backupStorage.primaryUsername);
                $scope.policyModalInstance = $uibModal.open({
                    templateUrl: 'PolicyModal',
                    scope: $scope
                });
            }, function (data, status) {
                $scope.policies = undefined;
            });
    };

    $scope.openServicePolicyModal = function (serviceName, serviceStatus) {
        $scope.getStoredPolicies();
        $rootScope.currentServicename = serviceName;
        $rootScope.serviceDisableFlag = false;

        if (serviceStatus == 'Configured') {
            $rootScope.serviceDisableFlag = true;
        }

        serviceN = serviceName.replace("fabric:/", "");
        serviceN = serviceN.replace("/", "_");
        console.log("The service name is " + serviceN);
        clusterEndp = $rootScope.primaryClusterEndpoint.replace(":19000", ":19080");
        console.log("The cluster name is " + clusterEndp);
        Metro.dialog.open('#policyConfigModal');
        $rootScope.serviceConfigLoad = true;
        $rootScope.serviceNoPolicyFoundFlag = false;
        $scope.policies = undefined;
        $http.get('api/RestoreService/servicepolicies/' + clusterEndp + '/' + serviceN)
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

    $scope.openAppPolicyModal = function (appName, appStatus) {
        $scope.getStoredPolicies();
        $rootScope.currentAppname = appName;
        $rootScope.appDisableFlag = false;

        if (appStatus == 'Configured') {
            $rootScope.appDisableFlag = true;
        }

        appNameN = appName.replace("fabric:/", "");
        clusterEndp = $rootScope.primaryClusterEndpoint.replace(":19000", ":19080");
        Metro.dialog.open('#appPolicyConfigModal');
        $rootScope.appConfigLoad = true;
        $rootScope.appNoPolicyFoundFlag = false;
        $scope.apppolicies = undefined;
        $http.get('api/RestoreService/apppolicies/' + clusterEndp + '/' + appNameN)
            .then(function (data, status) {
                $scope.apppolicies = data.data;
                console.log($scope.apppolicies);
                $rootScope.apppolicies = $scope.apppolicies;
                $rootScope.appConfigLoad = false;
            }, function (data, status) {
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
        $location.path("/");
    }

    $scope.getAppsOnPrimaryCluster = function () {

        $rootScope.primaryClusterEndpoint = $scope.primaryClusterEndpoint;
        $rootScope.secondaryClusterEndpoint = $scope.secondaryClusterEndpoint;
        

        var primaryAddress = $scope.primaryClusterEndpoint;

        if (primaryAddress.includes("http://"))
            primaryAddress = primaryAddress.replace("http://", "");

        if (primaryAddress.includes("https://"))
            primaryAddress = primaryAddress.replace("https://", "");

        $scope.primaryClusterEndpoint = $rootScope.primaryClusterEndpoint = primaryAddress;

        var secondaryAddress = $scope.secondaryClusterEndpoint;

        if (secondaryAddress.includes("http://"))
            secondaryAddress = secondaryAddress.replace("http://", "");

        if (secondaryAddress.includes("https://"))
            secondaryAddress = secondaryAddress.replace("https://", "");

        $scope.secondaryClusterEndpoint = $rootScope.secondaryClusterEndpoint = secondaryAddress;


        $rootScope.primaryClusterThumbprint = $scope.primSecureThumbp;
        $rootScope.secondaryClusterThumbprint = $scope.secSecureThumbp;

        $rootScope.primaryClusterCommonName = $scope.primaryCommonName;
        $rootScope.secondaryClusterCommonName = $scope.secondaryCommonName;


        $location.path("/servConfig");
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
            }, function (data, status) {
                $scope.apps = undefined;
                $rootScope.splashLoad = false;
                runToast('Please check the cluster details and try again', 'alert');
            });

        $scope.getStoredPolicies();

    };

    $scope.getStoredPolicies = function () {
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



    $scope.getAppsSecure = function () {
        $rootScope.primaryClusterEndpoint = $scope.primaryClusterEndpoint;
        $rootScope.secondaryClusterEndpoint = $scope.secondaryClusterEndpoint;

        var primaryAddress = $scope.primaryClusterEndpoint;

        if (primaryAddress.includes("http://"))
            primaryAddress = primaryAddress.replace("http://", "");

        if (primaryAddress.includes("https://"))
            primaryAddress = primaryAddress.replace("https://", "");

        $scope.primaryClusterEndpoint = $rootScope.primaryClusterEndpoint = primaryAddress;

        $http.get('api/RestoreService/' + $scope.primaryClusterEndpoint + '/' + $scope.primSecureThumbp)
            .then(function (data, status) {
                $scope.apps = data;
                console.log(data);
            }, function (data, status) {
                $scope.apps = undefined;
                window.alert('Please check the cluster details and try again');
            });

    }

}]);