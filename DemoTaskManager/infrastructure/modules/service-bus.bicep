param location string
param tags object
param namespaceName string
param queueName string

@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param skuName string = 'Standard'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Local auth stays enabled so developers can use a connection string; the deployed
    // App Service authenticates with its managed identity instead.
    disableLocalAuth: false
  }
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 5
    defaultMessageTimeToLive: 'P7D'
    deadLetteringOnMessageExpiration: true
    // The API sets MessageId from task id + correlation id, so duplicate submissions
    // of the same command are dropped by the broker.
    requiresDuplicateDetection: skuName != 'Basic'
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enablePartitioning: false
  }
}

output namespaceName string = serviceBusNamespace.name
output namespaceId string = serviceBusNamespace.id
output fullyQualifiedNamespace string = replace(replace(serviceBusNamespace.properties.serviceBusEndpoint, 'https://', ''), ':443/', '')
output queueName string = queue.name
