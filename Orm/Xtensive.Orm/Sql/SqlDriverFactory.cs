// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.06.23

using Xtensive.Core;
using Xtensive.Sql.Compiler;

namespace Xtensive.Sql
{
  /// <summary>
  /// Creates drivers from the specified connection info.
  /// </summary>
  public abstract class SqlDriverFactory
  {
    /// <summary>
    /// Creates driver from the specified <paramref name="connectionInfo"/>.
    /// </summary>
    /// <param name="connectionInfo">The connection info to create driver from.</param>
    /// <returns>Created driver.</returns>
    public SqlDriver CreateDriver(ConnectionInfo connectionInfo)
    {
      ArgumentValidator.EnsureArgumentNotNull(connectionInfo, "connectionInfo");
      var connectionString = connectionInfo.ConnectionString
        ?? BuildConnectionString(connectionInfo.ConnectionUrl);
      var driver = CreateDriver(connectionString);
      driver.Initialize(this);
      return driver;
    }

    /// <summary>
    /// Creates the driver from the specified <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">The connection string to create driver from.</param>
    /// <returns>Created driver.</returns>
    protected abstract SqlDriver CreateDriver(string connectionString);

    /// <summary>
    /// Builds the connection string from the specified URL.
    /// </summary>
    /// <param name="connectionUrl">The connection URL.</param>
    /// <returns>Built connection string</returns>
    public abstract string BuildConnectionString(UrlInfo connectionUrl);
  }
}