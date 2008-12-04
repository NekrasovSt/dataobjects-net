// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Kochetov
// Created:    2008.11.11

using System;
using System.Linq.Expressions;

namespace Xtensive.Storage.Rse.Compilation.Expressions
{
  public sealed class ColumnAccessExpression : ExtendedExpression
  {
    public int ColumnIndex { get; private set; }

    public override string ToString()
    {
      return string.Format("Column[{0}]", ColumnIndex);
    }


    public ColumnAccessExpression(Type type, int columnIndex)
      : base(ExtendedExpressionType.FieldAccess, type)
    {
      ColumnIndex = columnIndex;
    }
  }
}