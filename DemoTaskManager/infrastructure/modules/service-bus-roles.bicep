@description('Existing Service Bus namespace that receives the role assignments.')
param namespaceName string

@description('Object id of the App Service system-assigned managed identity.')
param principalId string

// Built-in role definition ids. Least privilege: the App Service only needs to send and
// receive on this namespace, never to manage it.
var dataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var dataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: namespaceName
}

resource dataSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBusNamespace
  name: guid(serviceBusNamespace.id, principalId, dataSenderRoleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dataSenderRoleId)
  }
}

resource dataReceiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBusNamespace
  name: guid(serviceBusNamespace.id, principalId, dataReceiverRoleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dataReceiverRoleId)
  }
}

output roleAssignmentIds array = [
  dataSender.id
  dataReceiver.id
]
