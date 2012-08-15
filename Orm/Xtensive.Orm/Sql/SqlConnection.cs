// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;
#if NET40
using System.Collections.Concurrent;
#endif
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xtensive.Core;
using Xtensive.Sql.Info;

namespace Xtensive.Sql
{
  /// <summary>
  /// A connection to a database.
  /// </summary>
  public abstract class SqlConnection : SqlDriverBound,
    IDisposable
  {
#if NET40
    private static readonly ConcurrentDictionary<SqlConnection, string> activeConnections = new ConcurrentDictionary<SqlConnection, string>();
#endif

    private int? commandTimeout;

    public static void DumpConnections()
    {
#if NET40
      var connections = activeConnections.ToArray();

      if (connections.Length==0)
        return;

      var lines = connections.AsEnumerable().Select(c => c.Value).Concat(new[] {string.Empty});
      File.AppendAllLines("D:\\connections.log", lines);
#endif
    }

    /// <summary>
    /// Gets the underlying connection.
    /// </summary>
    public abstract DbConnection UnderlyingConnection { get; }

    /// <summary>
    /// Gets the active transaction.
    /// </summary>
    public abstract DbTransaction ActiveTransaction { get; }

    /// <summary>
    /// Gets or sets the command timeout.
    /// </summary>
    public int? CommandTimeout
    {
      get { return commandTimeout; }
      set {
        if (value!=null)
          ArgumentValidator.EnsureArgumentIsInRange(value.Value, 0, 65535, "CommandTimeout");
        commandTimeout = value;
      }
    }

    /// <summary>
    /// Gets the state of the connection.
    /// </summary>
    public ConnectionState State { get { return UnderlyingConnection.State; } }
    
    /// <summary>
    /// Creates and returns a <see cref="DbCommand"/> object associated with the current connection.
    /// </summary>
    /// <returns>Created command.</returns>
    public DbCommand CreateCommand()
    {
      var command = CreateNativeCommand();
      if (commandTimeout!=null)
        command.CommandTimeout = commandTimeout.Value;
      command.Transaction = ActiveTransaction;
      return command;
    }
    
    /// <summary>
    /// Creates and returns a <see cref="DbCommand"/> object with specified <paramref name="statement"/>.
    /// Created command will be associated with the current connection.
    /// </summary>
    /// <returns>Created command.</returns>
    public DbCommand CreateCommand(ISqlCompileUnit statement)
    {
      ArgumentValidator.EnsureArgumentNotNull(statement, "statement");
      var command = CreateCommand();
      command.CommandText = Driver.Compile(statement).GetCommandText();
      return command;
    }
    
    /// <summary>
    /// Creates and returns a <see cref="DbCommand"/> object with specified <paramref name="commandText"/>.
    /// Created command will be associated with the current connection.
    /// </summary>
    /// <returns>Created command.</returns>
    public DbCommand CreateCommand(string commandText)
    {
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(commandText, "commandText");
      var command = CreateCommand();
      command.CommandText = commandText;
      return command;
    }

    /// <summary>
    /// Creates the parameter.
    /// </summary>
    /// <returns>Created parameter.</returns>
    public abstract DbParameter CreateParameter();
    
    /// <summary>
    /// Creates the cursor parameter.
    /// </summary>
    /// <returns>Created parameter.</returns>
    public virtual DbParameter CreateCursorParameter()
    {
      throw SqlHelper.NotSupported(ServerFeatures.CursorParameters);
    }

    /// <summary>
    /// Creates the character large object bound to this connection.
    /// Created object initially have NULL value (<see cref="ILargeObject.IsNull"/> returns <see langword="true"/>)
    /// </summary>
    /// <returns>Created CLOB.</returns>
    public virtual ICharacterLargeObject CreateCharacterLargeObject()
    {
      throw SqlHelper.NotSupported(ServerFeatures.LargeObjects);
    }

    /// <summary>
    /// Creates the binary large object bound to this connection.
    /// Created object initially have NULL value (<see cref="ILargeObject.IsNull"/> returns <see langword="true"/>)
    /// </summary>
    /// <returns>Created BLOB.</returns>
    public virtual IBinaryLargeObject CreateBinaryLargeObject()
    {
      throw SqlHelper.NotSupported(ServerFeatures.LargeObjects);
    }

    /// <summary>
    /// Opens the connection.
    /// </summary>
    public virtual void Open()
    {
      UnderlyingConnection.Open();
    }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    public virtual void Close()
    {
      UnderlyingConnection.Close();
    }

    /// <summary>
    /// Begins the transaction.
    /// </summary>
    public abstract void BeginTransaction();

    /// <summary>
    /// Begins the transaction with the specified <see cref="IsolationLevel"/>.
    /// </summary>
    /// <param name="isolationLevel">The isolation level.</param>
    public abstract void BeginTransaction(IsolationLevel isolationLevel);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    public virtual void Commit()
    {
      EnsureTransactionIsActive();
      try {
        ActiveTransaction.Commit();
      }
      finally {
        ActiveTransaction.Dispose();
        ClearActiveTransaction();
      }
    }

    /// <summary>
    /// Rollbacks the current transaction.
    /// </summary>
    public virtual void Rollback()
    {
      EnsureTransactionIsActive();
      try {
        ActiveTransaction.Rollback();
      }
      finally {
        ActiveTransaction.Dispose();
        ClearActiveTransaction();
      }      
    }

    /// <summary>
    /// Makes the transaction savepoint.
    /// </summary>
    /// <param name="name">The name of the savepoint.</param>
    public virtual void MakeSavepoint(string name)
    {
      // That's ok to make a savepoint even if they aren't supported - 
      // default impl. will fail on rollback
      return;
    }

    /// <summary>
    /// Rollbacks current transaction to the specified savepoint.
    /// </summary>
    /// <param name="name">The name of the savepoint.</param>
    public virtual void RollbackToSavepoint(string name)
    {
      throw SqlHelper.NotSupported(ServerFeatures.Savepoints);
    }

    /// <summary>
    /// Releases the savepoint with the specfied name.
    /// </summary>
    /// <param name="name">The name of the savepoint.</param>
    public virtual void ReleaseSavepoint(string name)
    {
      // That's ok to release a savepoint even if they aren't supported - 
      // default impl. will fail on rollback
      return;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
#if NET40
      string dummy;
      activeConnections.TryRemove(this, out dummy);
#endif

      if (ActiveTransaction!=null) {
        ActiveTransaction.Dispose();
        ClearActiveTransaction();
      }
      UnderlyingConnection.Dispose();
    }

    /// <summary>
    /// Clears the active transaction (i.e. sets <see cref="ActiveTransaction"/> to <see langword="null"/>.
    /// </summary>
    protected abstract void ClearActiveTransaction();
    
    /// <summary>
    /// Creates the native command.
    /// </summary>
    /// <returns>Created command.</returns>
    protected virtual DbCommand CreateNativeCommand()
    {
      return UnderlyingConnection.CreateCommand();
    }

    /// <summary>
    /// Ensures the transaction is active (i.e. <see cref="ActiveTransaction"/> is not <see langword="null"/>).
    /// </summary>
    protected void EnsureTransactionIsActive()
    {
      if (ActiveTransaction==null)
        throw new InvalidOperationException(Strings.ExTransactionShouldBeActive);
    }

    /// <summary>
    /// Ensures the trasaction is not active (i.e. <see cref="ActiveTransaction"/> is <see langword="null"/>).
    /// </summary>
    protected void EnsureTrasactionIsNotActive()
    {
      if (ActiveTransaction!=null)
        throw new InvalidOperationException(Strings.ExTransactionShouldNotBeActive);
    }


    // Constructors

    protected SqlConnection(SqlDriver driver, string connectionString)
      : base(driver)
    {
#if NET40
      activeConnections.TryAdd(this, new StackTrace().ToString());
#endif
    }
  }
}