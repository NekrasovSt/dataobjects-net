// Copyright (C) 2009-2020 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.
// Created by: Denis Krjuchkov
// Created:    2009.08.14

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xtensive.Core;
using Xtensive.Orm.Logging;
using Xtensive.Orm.Configuration;
using Xtensive.Orm.Model;
using Xtensive.Sql;
using Xtensive.Sql.Compiler;
using Xtensive.Sql.Info;
using Xtensive.Sql.Model;
using Xtensive.Tuples;

namespace Xtensive.Orm.Providers
{
  /// <summary>
  /// SQL storage driver.
  /// </summary>
  public sealed partial class StorageDriver
  {
    private readonly DomainConfiguration configuration;
    private readonly SqlDriver underlyingDriver;
    private readonly SqlTranslator translator;
    private readonly TypeMappingRegistry allMappings;
    private readonly bool isLoggingEnabled;
    private readonly bool hasSavepoints;

    public ProviderInfo ProviderInfo { get; private set; }

    public StorageExceptionBuilder ExceptionBuilder { get; private set; }

    public ServerInfo ServerInfo { get; private set; }

    public string BuildBatch(string[] statements)
    {
      return translator.BuildBatch(statements);
    }

    public string BuildParameterReference(string parameterName)
    {
      return translator.ParameterPrefix + parameterName;
    }

    public DefaultSchemaInfo GetDefaultSchema(SqlConnection connection) =>
      underlyingDriver.GetDefaultSchema(connection);

    public Task<DefaultSchemaInfo> GetDefaultSchemaAsync(SqlConnection connection, CancellationToken token) =>
      underlyingDriver.GetDefaultSchemaAsync(connection, token);

    public SqlExtractionResult Extract(SqlConnection connection, IEnumerable<SqlExtractionTask> tasks)
    {
      var result = underlyingDriver.Extract(connection, tasks);
      FixExtractionResult(result);
      return result;
    }

    public async Task<SqlExtractionResult> ExtractAsync(
      SqlConnection connection, IEnumerable<SqlExtractionTask> tasks, CancellationToken token)
    {
      var result = await underlyingDriver.ExtractAsync(connection, tasks, token).ConfigureAwait(false);
      FixExtractionResult(result);
      return result;
    }

    public SqlCompilationResult Compile(ISqlCompileUnit statement)
    {
      var options = new SqlCompilerConfiguration {
        DatabaseQualifiedObjects = configuration.IsMultidatabase
      };
      return underlyingDriver.Compile(statement, options);
    }

    public SqlCompilationResult Compile(ISqlCompileUnit statement, NodeConfiguration nodeConfiguration)
    {
      SqlCompilerConfiguration options;
      if (configuration.ShareStorageSchemaOverNodes)
        options = new SqlCompilerConfiguration(nodeConfiguration.GetDatabaseMapping(), nodeConfiguration.GetSchemaMapping());
      else
        options = new SqlCompilerConfiguration();
      options.DatabaseQualifiedObjects = configuration.IsMultidatabase;
      return underlyingDriver.Compile(statement, options);
    }

    public DbDataReaderAccessor GetDataReaderAccessor(TupleDescriptor descriptor)
    {
      return new DbDataReaderAccessor(descriptor, descriptor.Select(GetTypeMapping));
    }

    public StorageDriver CreateNew(Domain domain)
    {
      ArgumentValidator.EnsureArgumentNotNull(domain, "domain");
      return new StorageDriver(underlyingDriver, ProviderInfo, domain.Configuration, GetModelProvider(domain));
    }

    private static DomainModel GetNullModel()
    {
      return null;
    }

    private static Func<DomainModel> GetModelProvider(Domain domain)
    {
      return () => domain.Model;
    }

    private void FixExtractionResult(SqlExtractionResult result)
    {
      switch (ProviderInfo.ProviderName) {
      case WellKnown.Provider.SqlServer:
      case WellKnown.Provider.SqlServerCe:
        FixExtractionResultSqlServerFamily(result);
        break;
      case WellKnown.Provider.Sqlite:
        FixExtractionResultSqlite(result);
        break;
      }
    }

    private void FixExtractionResultSqlite(SqlExtractionResult result)
    {
      var tablesToFix =
        result.Catalogs
          .SelectMany(c => c.Schemas)
          .SelectMany(s => s.Tables)
          .Where(t => t.Name.EndsWith("-Generator", StringComparison.Ordinal)
            && t.TableColumns.Count==1
            && t.TableColumns[0].SequenceDescriptor==null);

      foreach (var table in tablesToFix) {
        var column = table.TableColumns[0];
        column.SequenceDescriptor = new SequenceDescriptor(column, 1, 1);
      }
    }

    private void FixExtractionResultSqlServerFamily(SqlExtractionResult result)
    {
      // Don't bother about tables for diagramming

      foreach (var schema in result.Catalogs.SelectMany(c => c.Schemas)) {
        var tables = schema.Tables;
        var sysdiagrams = tables["sysdiagrams"];
        if (sysdiagrams!=null)
          tables.Remove(sysdiagrams);
      }
    }

    // Constructors

    public static StorageDriver Create(SqlDriverFactory driverFactory, DomainConfiguration configuration)
    {
      ArgumentValidator.EnsureArgumentNotNull(driverFactory, nameof(driverFactory));
      ArgumentValidator.EnsureArgumentNotNull(configuration, nameof(configuration));

      var driverConfiguration = new SqlDriverConfiguration {
        ForcedServerVersion = configuration.ForcedServerVersion,
        ConnectionInitializationSql = configuration.ConnectionInitializationSql,
        EnsureConnectionIsAlive = configuration.EnsureConnectionIsAlive,
      };

      var driver = driverFactory.GetDriver(configuration.ConnectionInfo, driverConfiguration);
      var providerInfo = ProviderInfoBuilder.Build(configuration.ConnectionInfo.Provider, driver);

      return new StorageDriver(driver, providerInfo, configuration, GetNullModel);
    }

    public static async Task<StorageDriver> CreateAsync(
      SqlDriverFactory driverFactory, DomainConfiguration configuration, CancellationToken token)
    {
      ArgumentValidator.EnsureArgumentNotNull(driverFactory, nameof(driverFactory));
      ArgumentValidator.EnsureArgumentNotNull(configuration, nameof(configuration));

      var driverConfiguration = new SqlDriverConfiguration {
        ForcedServerVersion = configuration.ForcedServerVersion,
        ConnectionInitializationSql = configuration.ConnectionInitializationSql,
        EnsureConnectionIsAlive = configuration.EnsureConnectionIsAlive,
      };

      var driver = await driverFactory.GetDriverAsync(configuration.ConnectionInfo, driverConfiguration, token)
        .ConfigureAwait(false);
      var providerInfo = ProviderInfoBuilder.Build(configuration.ConnectionInfo.Provider, driver);

      return new StorageDriver(driver, providerInfo, configuration, GetNullModel);
    }

    private StorageDriver(
      SqlDriver driver, ProviderInfo providerInfo, DomainConfiguration configuration, Func<DomainModel> modelProvider)
    {
      underlyingDriver = driver;
      ProviderInfo = providerInfo;
      this.configuration = configuration;
      ExceptionBuilder = new StorageExceptionBuilder(driver, configuration, modelProvider);
      allMappings = underlyingDriver.TypeMappings;
      translator = underlyingDriver.Translator;
      hasSavepoints = underlyingDriver.ServerInfo.ServerFeatures.Supports(ServerFeatures.Savepoints);
      isLoggingEnabled = SqlLog.IsLogged(LogLevel.Info); // Just to cache this value
      ServerInfo = underlyingDriver.ServerInfo;
    }
  }
}