// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2009.05.06

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using Xtensive.Core.Collections;
using Xtensive.Storage.Rse.Providers;

namespace Xtensive.Storage.Linq.Expressions.Visitors
{
  internal class ColumnGatherer : PersistentExpressionVisitor
  {
    private readonly ColumnExtractionModes columnExtractionModes;
    private readonly List<int> columns = new List<int>();
    private SubQueryExpression topSubquery;

    private bool TreatEntityAsKey
    {
      get { return (columnExtractionModes & ColumnExtractionModes.TreatEntityAsKey)!=ColumnExtractionModes.Default; }
    }

    private bool KeepTypeId
    {
      get { return (columnExtractionModes & ColumnExtractionModes.KeepTypeId)!=ColumnExtractionModes.Default; }
    }

    private bool DistinctValues
    {
      get { return (columnExtractionModes & ColumnExtractionModes.Distinct)!=ColumnExtractionModes.Default; }
    }

    private bool OrderedValues
    {
      get { return (columnExtractionModes & ColumnExtractionModes.Ordered)!=ColumnExtractionModes.Default; }
    }

    private bool OmitLazyLoad
    {
      get { return (columnExtractionModes & ColumnExtractionModes.OmitLazyLoad) != ColumnExtractionModes.Default; }
    }

    public static List<int> GetColumns(Expression expression, ColumnExtractionModes columnExtractionModes)
    {
      var gatherer = new ColumnGatherer(columnExtractionModes);
      gatherer.Visit(expression);
      var distinct = gatherer.DistinctValues
        ? gatherer.columns.Distinct()
        : gatherer.columns;
      var ordered = gatherer.OrderedValues
        ? distinct.OrderBy(i => i)
        : distinct;
      return ordered.ToList();
    }

    protected override Expression VisitMarker(MarkerExpression expression)
    {
      Visit(expression.Target);
      return expression;
    }

    protected override Expression VisitFieldExpression(FieldExpression f)
    {
      ProcessFieldOwner(f);
      AddColumns(f, f.Mapping.GetItems());
      return f;
    }

    protected override Expression VisitStructureExpression(StructureExpression s)
    {
      ProcessFieldOwner(s);
      AddColumns(s,
        s.Fields
          .Where(f => f.ExtendedType==ExtendedExpressionType.Field)
          .Select(f => f.Mapping.Offset));
      return s;
    }

    protected override Expression VisitKeyExpression(KeyExpression k)
    {
      AddColumns(k, k.Mapping.GetItems());
      return k;
    }

    protected override Expression VisitEntityExpression(EntityExpression e)
    {
      if (TreatEntityAsKey) {
        var keyExpression = (KeyExpression) e.Fields.First(f => f.ExtendedType==ExtendedExpressionType.Key);
        AddColumns(e, keyExpression.Mapping.GetItems());
        if (KeepTypeId)
          AddColumns(e, e.Fields.First(f => f.Name==WellKnown.TypeIdFieldName).Mapping.GetItems());
      }
      else {
        AddColumns(e,
          e.Fields
            .OfType<FieldExpression>()
            .Where(f => f.ExtendedType==ExtendedExpressionType.Field)
            .Where(f => !(OmitLazyLoad && f.LoadMode==FieldLoadMode.Lazy))
            .Select(f => f.Mapping.Offset));
      }
      return e;
    }

    protected override Expression VisitEntityFieldExpression(EntityFieldExpression ef)
    {
      var keyExpression = (KeyExpression) ef.Fields.First(f => f.ExtendedType==ExtendedExpressionType.Key);
      AddColumns(ef, keyExpression.Mapping.GetItems());
      if (!TreatEntityAsKey)
        Visit(ef.Entity);
      return ef;
    }

    protected override Expression VisitEntitySetExpression(EntitySetExpression es)
    {
      VisitEntityExpression((EntityExpression) es.Owner);
      return es;
    }

    protected override Expression VisitColumnExpression(ColumnExpression c)
    {
      AddColumns(c, c.Mapping.GetItems());
      return c;
    }

    protected override Expression VisitGroupingExpression(GroupingExpression expression)
    {
      Visit(expression.KeyExpression);
      VisitSubQueryExpression(expression);
      return expression;
    }

    protected override Expression VisitSubQueryExpression(SubQueryExpression subQueryExpression)
    {
      bool isTopSubquery = false;

      if (topSubquery==null) {
        isTopSubquery = true;
        topSubquery = subQueryExpression;
      }

      Visit(subQueryExpression.ProjectionExpression.ItemProjector.Item);
      var visitor = new ApplyParameterAccessVisitor(topSubquery.ApplyParameter, (mc, index) => {
        columns.Add(index);
        return mc;
      });
      var providerVisitor = new CompilableProviderVisitor((provider, expression) => visitor.Visit(expression));
      providerVisitor.VisitCompilable(subQueryExpression.ProjectionExpression.ItemProjector.DataSource.Provider);

      if (isTopSubquery)
        topSubquery = null;

      return subQueryExpression;
    }

    private void ProcessFieldOwner(FieldExpression f)
    {
      if (TreatEntityAsKey || f.Owner==null)
        return;
      var entity = f.Owner as EntityExpression;
      var structure = f.Owner as StructureExpression;
      while (entity==null && structure!=null) {
        entity = structure.Owner as EntityExpression;
        structure = structure.Owner as StructureExpression;
      }
      if (entity==null)
        throw new InvalidOperationException("Unable to resolve owner.");

      AddColumns(f,
        entity
          .Key
          .Mapping
          .GetItems()
          .AddOne(entity
            .Fields
            .Single(field => field.Name==WellKnown.TypeIdFieldName)
            .Mapping
            .Offset));
    }

    private void AddColumns(ParameterizedExpression parameterizedExpression, IEnumerable<int> expressionColumns)
    {
      var isSubqueryParameter = topSubquery!=null && parameterizedExpression.OuterParameter==topSubquery.OuterParameter;
      var isNotParametrized = topSubquery==null && parameterizedExpression.OuterParameter==null;

      if (isSubqueryParameter || isNotParametrized)
        columns.AddRange(expressionColumns);
    }

    // Constructors

    private ColumnGatherer(ColumnExtractionModes columnExtractionModes)
    {
      this.columnExtractionModes = columnExtractionModes;
    }
  }
}