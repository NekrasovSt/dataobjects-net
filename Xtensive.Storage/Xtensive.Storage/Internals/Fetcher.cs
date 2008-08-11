// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2008.07.17

using System.Collections.Generic;
using System.Linq;
using Xtensive.Storage.Model;
using Xtensive.Storage.Rse;
using Xtensive.Storage.Rse.Providers.Compilable;

namespace Xtensive.Storage.Internals
{
  internal static class Fetcher
  {
    public static void Fetch(Key key)
    {
      IndexInfo index = key.Type.Indexes.PrimaryIndex;
      Fetch(index, key, index.Columns.Where(c => !c.LazyLoad));
    }

    public static void Fetch(Key key, FieldInfo field)
    {
      // Fetching all non-lazyload fields instead of exactly one non-lazy load field.
      if (!field.LazyLoad) {
        Fetch(key);
        return;
      }

      IndexInfo index = key.Type.Indexes.PrimaryIndex;
      IEnumerable<ColumnInfo> columns = key.Type.Columns
        .Where(c => c.IsPrimaryKey || c.IsSystem)
        .Union(key.Type.Columns
          .Skip(field.MappingInfo.Offset)
          .Take(field.MappingInfo.Length));
      Fetch(index, key, columns);
    }

    /// <inheritdoc/>
    public static void Fetch(IndexInfo index, Key key, IEnumerable<ColumnInfo> columns)
    {
      new IndexProvider(index).Result
        .Range(key.Tuple, key.Tuple)
        .Select(columns.Select(c => index.Columns.IndexOf(c)).ToArray())
        .Take(1)
        .Process();
    }
  }
}
