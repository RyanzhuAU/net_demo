targetScope = 'resourceGroup'

@description('Short application name used to derive resource names.')
@minLength(3)
@maxLength(12)
param applicationName string = 'demotask'

@description('Environment discriminator, for example dev, test or prod.')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'dev'

@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('App Service plan SKU. Windows plan is required for the run.cmd WebJob entry points.')
@allowed([
  'B1'
  'S1'
  'P0v3'
  'P1v3'
])
param appServicePlanSku string = 'S1'

@description('SQL administrator login. Supply through a pipeline variable, never in source control.')
@minLength(4)
param sqlAdministratorLogin string

@description('SQL administrator password. Supply from a variable group secret or Key Vault.')
@secure()
@minLength(12)
param sqlAdministratorPassword string

@description('Entra ID object id that becomes the SQL Entra administrator. Leave empty to skip.')
param sqlEntraAdminObjectId string = ''

@description('Display name matching sqlEntraAdminObjectId.')
param sqlEntraAdminLogin string = ''

@description('Azure SQL database SKU name.')
param sqlDatabaseSkuName string = 'S0'

@description('Service Bus queue consumed by the continuous WebJob.')
param serviceBusQueueName string = 'task-processing'

@description('NCRONTAB schedule (6 fields) applied to the triggered WebJob.')
param scheduledWebJobCron string = '0 */15 * * * *'

@description('Tags applied to every resource.')
param tags object = {
  application: applicationName
  environment: environmentName
  managedBy: 'bicep'
}

var uniqueSuffix = take(uniqueString(resourceGroup().id, applicationName, environmentName), 6)
var resourceBaseName = toLower('${applicationName}-${environmentName}-${uniqueSuffix}')

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    tags: tags
    logAnalyticsName: 'log-${resourceBaseName}'
    applicationInsightsName: 'appi-${resourceBaseName}'
  }
}

module serviceBus 'modules/service-bus.bicep' = {
  name: 'service-bus'
  params: {
    location: location
    tags: tags
    namespaceName: 'sb-${resourceBaseName}'
    queueName: serviceBusQueueName
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    tags: tags
    serverName: 'sql-${resourceBaseName}'
    databaseName: 'sqldb-${applicationName}-${environmentName}'
    administratorLogin: sqlAdministratorLogin
    administratorPassword: sqlAdministratorPassword
    entraAdminObjectId: sqlEntraAdminObjectId
    entraAdminLogin: sqlEntraAdminLogin
    databaseSkuName: sqlDatabaseSkuName
  }
}

module appService 'modules/app-service.bicep' = {
  name: 'app-service'
  params: {
    location: location
    tags: tags
    appServicePlanName: 'plan-${resourceBaseName}'
    appServiceName: 'app-${resourceBaseName}'
    appServicePlanSku: appServicePlanSku
    environmentName: environmentName
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    serviceBusFullyQualifiedNamespace: serviceBus.outputs.fullyQualifiedNamespace
    serviceBusQueueName: serviceBusQueueName
    scheduledWebJobCron: scheduledWebJobCron
    sqlConnectionString: sql.outputs.connectionStringTemplate
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorPassword: sqlAdministratorPassword
  }
}

module serviceBusRoles 'modules/service-bus-roles.bicep' = {
  name: 'service-bus-roles'
  params: {
    namespaceName: serviceBus.outputs.namespaceName
    principalId: appService.outputs.principalId
  }
}

@description('Default hostname of the deployed App Service.')
output appServiceDefaultHostName string = appService.outputs.defaultHostName

@description('Name of the App Service, required by the deployment pipeline.')
output appServiceName string = appService.outputs.name

@description('Name of the App Service plan.')
output appServicePlanName string = appService.outputs.planName

@description('Health probe URL used by the pipeline smoke test.')
output healthCheckUrl string = 'https://${appService.outputs.defaultHostName}/health'

@description('Service Bus namespace name.')
output serviceBusNamespaceName string = serviceBus.outputs.namespaceName

@description('Fully qualified Service Bus namespace used for managed identity authentication.')
output serviceBusFullyQualifiedNamespace string = serviceBus.outputs.fullyQualifiedNamespace

@description('Service Bus queue name.')
output serviceBusQueueName string = serviceBusQueueName

@description('Logical SQL server fully qualified domain name.')
output sqlServerFqdn string = sql.outputs.serverFqdn

@description('SQL database name, required by the migration step.')
output sqlDatabaseName string = sql.outputs.databaseName

@description('Application Insights resource name.')
output applicationInsightsName string = monitoring.outputs.applicationInsightsName

@description('Role assignment ids created for the App Service managed identity.')
output serviceBusRoleAssignmentIds array = serviceBusRoles.outputs.roleAssignmentIds
