param location string
param tags object
param appServicePlanName string
param appServiceName string
param appServicePlanSku string
param environmentName string
param applicationInsightsConnectionString string
param serviceBusFullyQualifiedNamespace string
param serviceBusQueueName string
param scheduledWebJobCron string
param sqlConnectionString string
param sqlAdministratorLogin string

@secure()
param sqlAdministratorPassword string

var aspNetCoreEnvironment = environmentName == 'prod' ? 'Production' : (environmentName == 'test' ? 'Staging' : 'Development')

// A Windows plan is required: both WebJobs are launched through run.cmd.
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: appServicePlanSku
  }
  kind: 'app'
  properties: {
    reserved: false
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceName
  location: location
  tags: tags
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      alwaysOn: appServicePlanSku != 'B1'
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      webSocketsEnabled: false
      use32BitWorkerProcess: false
    }
  }
}

resource appSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: appService
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: aspNetCoreEnvironment
    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString
    ApplicationInsightsAgent_EXTENSION_VERSION: '~3'
    // WebJobs need a writable App_Data folder, so run-from-package is intentionally off.
    WEBSITE_RUN_FROM_PACKAGE: '0'
    WEBJOBS_IDLE_TIMEOUT: '600'
    Spa__ApiBaseUrl: '/api'
    Spa__ApplicationInsightsConnectionString: applicationInsightsConnectionString
    ServiceBus__QueueName: serviceBusQueueName
    ServiceBus__FullyQualifiedNamespace: serviceBusFullyQualifiedNamespace
    ServiceBus__Enabled: 'true'
    // Consumed by the Azure WebJobs SDK ServiceBus trigger with managed identity.
    ServiceBusConnection__fullyQualifiedNamespace: serviceBusFullyQualifiedNamespace
    Reminders__BatchSize: '100'
    Reminders__GraceMinutes: '0'
    WebJobs__ScheduledCron: scheduledWebJobCron
    Database__AutoMigrate: 'false'
  }
}

resource connectionStrings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: appService
  name: 'connectionstrings'
  properties: {
    DefaultConnection: {
      // Credentials arrive as secure deployment parameters and are stored by App Service
      // as a slot setting; they are never written to logs or outputs.
      value: '${sqlConnectionString}User ID=${sqlAdministratorLogin};Password=${sqlAdministratorPassword};'
      type: 'SQLAzure'
    }
  }
}

output name string = appService.name
output planName string = appServicePlan.name
output defaultHostName string = appService.properties.defaultHostName
output principalId string = appService.identity.principalId
