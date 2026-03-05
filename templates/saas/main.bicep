@description('The environment name (staging or prod)')
@allowed(['staging', 'prod'])
param environment string

@description('The project name')
param projectName string

@description('The location for all resources')
param location string = resourceGroup().location

@description('Enable Azure App Configuration')
param enableAppConfig bool = false

@description('Custom domain name for DNS zones')
param customDomain string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var suffix = '${projectName}-${environment}-${uniqueSuffix}'
var identityName = 'id-${suffix}'
var kvName = take('kv-${projectName}-${environment}-${uniqueSuffix}', 24)
var logWorkspaceName = 'log-${suffix}'
var appInsightsName = 'appi-${suffix}'
var vnetName = 'vnet-${suffix}'

// Tiers based on environment
var appServicePlanSku = (environment == 'prod') ? 'S1' : 'F1'
var sqlSku = (environment == 'prod') ? 'S0' : 'Basic'

// --- Managed Identity (A Must) ---
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

// --- Log Analytics & App Insights (Observability) ---
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logWorkspaceName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logWorkspace.id
  }
}

// --- Key Vault (Secure Secrets) ---
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: kvName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true // Modern approach
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

// --- Networking (VNet Integration) ---
resource vnet 'Microsoft.Network/virtualNetworks@2022-11-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: { addressPrefixes: ['10.0.0.0/16'] }
    subnets: [
      {
        name: 'snet-apps'
        properties: {
          addressPrefix: '10.0.1.0/24'
          delegations: [
            {
              name: 'delegation'
              properties: { serviceName: 'Microsoft.Web/serverfarms' }
            }
          ]
        }
      }
      {
        name: 'snet-endpoints'
        properties: { addressPrefix: '10.0.2.0/24' }
      }
    ]
  }
}

// --- DNS Zones ---
resource privateDnsZoneKV 'Microsoft.Network/privateDnsZones@2020-06-01' = if (!empty(customDomain)) {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
}

// --- App Service Plan ---
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'asp-${suffix}'
  location: location
  sku: { name: appServicePlanSku }
  kind: 'linux'
  properties: { reserved: true }
}

// --- Web App (Blazor) ---
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: 'app-${suffix}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${managedIdentity.id}': {} }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: vnet.properties.subnets[0].id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsights.properties.InstrumentationKey }
        { name: 'AZURE_CLIENT_ID', value: managedIdentity.properties.clientId }
        { name: 'KEYVAULT_URL', value: keyVault.properties.vaultUri }
      ]
    }
  }
}

// --- Optional App Config ---
resource appConfig 'Microsoft.AzConfiguration/configurationStores@2023-03-01' = if (enableAppConfig) {
  name: 'appcfg-${suffix}'
  location: location
  sku: { name: 'free' }
}

output keyVaultUri string = keyVault.properties.vaultUri
output identityClientId string = managedIdentity.properties.clientId
