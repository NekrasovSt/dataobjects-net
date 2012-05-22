// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Malisa Ncube
// Created:    2011.04.29

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xtensive.Sql.Model;

namespace Xtensive.Sql.Drivers.Sqlite.v3
{
  internal class Extractor : Model.Extractor
  {
    public static readonly string PrimaryKeyName = "PrimaryKey";

    private const int DefaultPrecision = 38;
    private const int DefaultScale = 0;
    private const string SqliteSequence = "sqlite_sequence";
    private const string SqliteMaster = "sqlite_master";

    private Schema schema;
    private Catalog catalog;

    protected override void Initialize()
    {
      catalog = new Catalog(string.Empty);
      schema = catalog.CreateSchema(Driver.CoreServerInfo.DefaultSchemaName);
    }

    /// <inheritdoc/>
    public override Catalog ExtractCatalog(string catalogName)
    {
      ExtractCatalogContents();
      return catalog;
    }

    /// <inheritdoc/>
    public override Schema ExtractSchema(string catalogName, string schemaName)
    {
      ExtractCatalogContents();
      return catalog.Schemas.Single();
    }

    private void ExtractCatalogContents()
    {
      ExtractTables();
      ExtractViews();
      ExtractColumns();
      ExtractIndexes();
      ExtractForeignKeys();
    }

    private void ExtractTables()
    {
      const string query = "SELECT [name] FROM [Main].[sqlite_master] WHERE type = 'table' AND name NOT LIKE 'sqlite?_%' ESCAPE '?'";
      using (var cmd = Connection.CreateCommand(query))
      using (IDataReader reader = cmd.ExecuteReader()) {
        while (reader.Read()) {
          schema.CreateTable(reader.GetString(0));
        }
      }
    }

    public bool DoesTableExist(string tableName)
    {
      var select = string.Format("SELECT name FROM {0} WHERE type = 'table' AND name='{1}'", SqliteMaster, tableName);
      using (var cmd = Connection.CreateCommand(select))
      using (IDataReader reader = cmd.ExecuteReader())
        return reader.Read();
    }

    private int? GetIncrementValue(string tableName)
    {
      if (!DoesTableExist(SqliteSequence))
        return null;

      var select = string.Format("SELECT seq from {0} WHERE name = '{1}' ", SqliteSequence, tableName);
      using (var cmd = Connection.CreateCommand(select))
      using (IDataReader reader = cmd.ExecuteReader()) {
        while (reader.Read())
          return ReadNullableInt(reader, "seq");
        return null;
      }
    }

    private void ExtractColumns()
    {
      foreach (var table in schema.Tables) {
        var select = string.Format("PRAGMA table_info([{0}])", table.Name);
        var primaryKeyItems = new List<TableColumn>();
        using (var cmd = Connection.CreateCommand(select))
        using (IDataReader reader = cmd.ExecuteReader()) {
          while (reader.Read()) {
            var tableSchema = table.Schema;
            string tableName = table.Name;

            //Column Name
            var tableColumn = table.CreateColumn(reader.GetString(1));

            //Column Type
            //var empty = reader.GetString(2);
            //var empty2 = empty;
            tableColumn.DataType = CreateValueType(reader, 2);

            //IsNullable
            tableColumn.IsNullable = ReadInt(reader, 3)==0;

            //Default Value
            var defaultValue = ReadStringOrNull(reader, 4);
            if (!string.IsNullOrEmpty(defaultValue) && string.Compare("NULL", defaultValue, StringComparison.OrdinalIgnoreCase)!=0)
              tableColumn.DefaultValue = defaultValue;

            var isPrimaryKey = ReadInt(reader, 5)==1;
            if (isPrimaryKey) {
              primaryKeyItems.Add(tableColumn);
              // Auto Increment
              var autoInc = GetIncrementValue(tableName);
              tableColumn.SequenceDescriptor = new SequenceDescriptor(tableColumn, autoInc, 1);
            }
          }
        }

        if (primaryKeyItems.Count > 0)
          table.CreatePrimaryKey(PrimaryKeyName, primaryKeyItems.ToArray());
      }
    }

    private IEnumerable<string> ColumnNamesFromIndex(string indexName)
    {
      var select = string.Format("PRAGMA index_info([{0}])", indexName);
      using (var cmd = Connection.CreateCommand(select))
      using (IDataReader reader = cmd.ExecuteReader()) {
        while (reader.Read())
          yield return ReadStringOrNull(reader, 2);
      }
    }

    private void ExtractViews()
    {
      const string select = "SELECT [name], sql FROM [Main].[sqlite_master] WHERE type = 'view' AND name NOT LIKE 'sqlite?_%' ESCAPE '?'";
      using (var reader = ExecuteReader(select)) {
        while (reader.Read()) {
          string view = reader.GetString(0);
          string definition = ReadStringOrNull(reader, 1);
          if (string.IsNullOrEmpty(definition))
            schema.CreateView(view);
          else
            schema.CreateView(view, SqlDml.Native(definition));
        }
      }
    }

    private void ExtractIndexes()
    {
      foreach (var table in schema.Tables) {
        var select = string.Format("PRAGMA index_list([{0}])", table.Name);
        using (var cmd = Connection.CreateCommand(select))
        using (IDataReader reader = cmd.ExecuteReader()) {
          while (reader.Read()) {
            var tableSchema = table.Schema;
            var tableName = table.Name;
            var indexName = ReadStringOrNull(reader, 1);
            if (indexName.StartsWith("sqlite_autoindex_")) {
              // Special index used for primary keys
              // It should be hidden here, because PK are already extracted in ExtractColumns()
              continue;
            }
            var unique = reader.GetBoolean(2);
            var index = table.CreateIndex(indexName);
            index.IsUnique = unique;
            foreach (var columnName in ColumnNamesFromIndex(indexName))
              index.CreateIndexColumn(table.TableColumns[columnName]);
          }
        }
      }
    }

    private void ExtractForeignKeys()
    {
      foreach (var table in schema.Tables) {
        var select = string.Format("PRAGMA foreign_key_list([{0}])", table.Name);

        int lastColumnPosition = int.MaxValue;
        ForeignKey constraint = null;
        Table referencingTable = null;
        Table referencedTable = null;

        using (var cmd = Connection.CreateCommand(select))
        using (IDataReader reader = cmd.ExecuteReader()) {
          ForeignKey foreignKey = null;
          while (reader.Read()) {
            var foreignKeyName = String.Format(CultureInfo.InvariantCulture, "FK_{0}_{1}", referencingTable.Name, ReadStringOrNull(reader, 2));

            int columnPosition = ReadInt(reader, 5);
            if (columnPosition <= lastColumnPosition) {
              referencingTable = table;
              constraint = referencingTable.CreateForeignKey(foreignKeyName);

              ReadCascadeAction(constraint, reader, 7);
              var referencedSchema = table.Schema; //Schema same as current
              referencedTable = referencedSchema.Tables[ReadStringOrNull(reader, 2)];
              constraint.ReferencedTable = referencedTable;
            }
            var referencingColumn = referencingTable.TableColumns[reader.GetString(3)];
            var referencedColumn = referencedTable.TableColumns[reader.GetString(4)];
            constraint.Columns.Add(referencingColumn);
            constraint.ReferencedColumns.Add(referencedColumn);
            lastColumnPosition = columnPosition;
          }
        }
      }
    }

    private ReferentialAction GetReferentialAction(string actionName)
    {
      if (actionName.ToUpper()=="SET NULL")
        return ReferentialAction.SetNull;
      if (actionName.ToUpper()=="SET DEFAULT")
        return ReferentialAction.SetDefault;
      if (actionName.StartsWith("CASCADE"))
        return ReferentialAction.Cascade;
      return ReferentialAction.NoAction;
    }

    private SqlValueType CreateValueType(IDataRecord row, int typeNameIndex) //, int precisionIndex, int scaleIndex, int charLengthIndex
    {
      string realType;
      int precision = 0;
      int scale = 0;

      string typeName = row.GetString(typeNameIndex);
      typeName = typeName.ToUpperInvariant();

      var spacePattern = new Regex(@"\s+");
      var numberPattern = new Regex(@"(?<DataType>\w+)\((?<Precision>\d+)(,\s?(?<Scale>\d+))?\)"); //to get TYPE(P, S) e.g. NUMBER(5,2)

      var spacelessNumber = spacePattern.Replace(typeName, @" ");
      var match = numberPattern.Match(spacelessNumber);
      if (match.Success) {
        realType = match.Groups["DataType"].Value;

        if (match.Groups["Precision"].Value!=string.Empty)
          precision = int.Parse(match.Groups["Precision"].Value);
        if (match.Groups["Scale"].Value!=string.Empty)
          scale = int.Parse(match.Groups["Scale"].Value);
      }
      else {
        realType = typeName;
        precision = 0;
        scale = 0;
      }

      if (realType=="NUMBER" || realType=="NUMERIC" || realType=="DOUBLE" || realType=="REAL" || realType=="FLOAT") {
        if (precision < scale)
          precision = scale; //cheat
        return new SqlValueType(SqlType.Decimal, precision, scale);
      }

      if (realType.Contains("INT") || realType.Contains("AUTOINC"))
        // ignoring details because of resulting affinity
        // although they can be read as "scale" and "precision"
        return new SqlValueType(SqlType.Int32);

      if (realType.StartsWith("CURRENCY"))
        // "current precision" is saved as "scale", ignoring too
        return new SqlValueType(SqlType.Double);

      if (realType.StartsWith("TIME"))
        // "timestamp precision" is saved as "scale", ignoring too
        return new SqlValueType(SqlType.DateTime);

      if (realType.StartsWith("BOOLEAN"))
        return new SqlValueType(SqlType.Boolean);

      if (realType.StartsWith("DATE"))
        // "timestamp precision" is saved as "scale", ignoring too
        return new SqlValueType(SqlType.DateTime);

      if (realType.Contains("MEMO") || realType.Contains("TEXT")) {
        precision = precision==0 ? int.MaxValue : precision;
        return new SqlValueType(SqlType.VarCharMax);
      }

      if (realType.Contains("BLOB") || realType.Contains("GRAPHIC") || realType.Contains("IMAGE"))
        return new SqlValueType(SqlType.Binary);

      if (realType=="CHAR" || realType=="NCHAR") {
        precision = precision==0 ? int.MaxValue : precision;
        return new SqlValueType(SqlType.Char, precision);
      }

      if (realType.Contains("CHAR")) {
        precision = precision==0 ? int.MaxValue : precision;
        return new SqlValueType(SqlType.VarChar, precision);
      }

      var typeInfo = Driver.ServerInfo.DataTypes[realType];

      return typeInfo!=null ? new SqlValueType(typeInfo.Type) : new SqlValueType(realType);
    }

    private static int ReadInt(IDataRecord row, int index)
    {
      decimal value = row.GetDecimal(index);
      return value > int.MaxValue ? int.MaxValue : (int) value;
    }

    private static string ReadStringOrNull(IDataRecord row, int index)
    {
      return row.IsDBNull(index) ? null : row.GetString(index);
    }

    private static int? ReadNullableInt(IDataRecord reader, string column)
    {
      return Convert.IsDBNull(reader[column]) ? null : (int?) Convert.ToInt32(reader[column]);
    }

    private static void ReadCascadeAction(ForeignKey foreignKey, IDataRecord row, int deleteRuleIndex)
    {
      var deleteRule = row.GetString(deleteRuleIndex);
      switch (deleteRule) {
      case "CASCADE":
        foreignKey.OnDelete = ReferentialAction.Cascade;
        return;
      case "SET NULL":
        foreignKey.OnDelete = ReferentialAction.SetNull;
        return;
      case "NO ACTION":
        foreignKey.OnDelete = ReferentialAction.NoAction;
        return;
      }
    }

    public Extractor(SqlDriver driver)
      : base(driver)
    {
    }
  }
}