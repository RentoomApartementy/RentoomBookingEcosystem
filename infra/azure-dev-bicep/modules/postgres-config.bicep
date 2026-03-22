targetScope = 'resourceGroup'

@description('Existing PostgreSQL Flexible Server name.')
param postgresServerName string

@description('Desired max_connections value on the PostgreSQL Flexible Server.')
@minValue(1)
param postgresMaxConnections int

resource existingPostgres 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' existing = {
  name: postgresServerName
}

resource postgresMaxConnectionsSetting 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2025-08-01' = {
  parent: existingPostgres
  name: 'max_connections'
  properties: {
    value: string(postgresMaxConnections)
    source: 'user-override'
  }
}
