// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Kochetov
// Created:    2009.01.13

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xtensive.Core.Linq;
using Xtensive.Core.Reflection;
using Xtensive.Core.Tuples;
using Xtensive.Core.Tuples.Transform;
using Xtensive.Storage.Linq.Expressions;
using Xtensive.Storage.Linq.Expressions.Visitors;
using Xtensive.Storage.Model;
using Xtensive.Storage.Rse;

namespace Xtensive.Storage.Linq
{
  internal class ProjectionBuilder : ExpressionVisitor
  {
    private readonly QueryTranslator translator;
    private ResultExpression source;
    private ParameterExpression tuple;
    private ParameterExpression record;
    private bool tupleIsUsed;
    private bool recordIsUsed;
    private RecordSet recordSet;
    private ResultMapping mapping;
    private static readonly MethodInfo transformApplyMethod;
    private static readonly MethodInfo keyCreateMethod;
    private static readonly MethodInfo selectMethod;
    private static readonly MethodInfo genericAccessor;
    private static readonly MethodInfo nonGenericAccessor;

    public ResultExpression Build(ResultExpression source, Expression body)
    {
      this.source = translator.FieldAccessBasedJoiner.Process(source, body, true);
      tuple = Expression.Parameter(typeof (Tuple), "t");
      record = Expression.Parameter(typeof (Record), "r");
      tupleIsUsed = false;
      recordIsUsed = false;
      recordSet = this.source.RecordSet;
      mapping = source.Mapping;
      Expression<Func<RecordSet, object>> lambda = null;

      var newBody = Visit(body);
      if (recordIsUsed) {
        // TODO: implement
      }
      else {
        var rs = Expression.Parameter(typeof(RecordSet), "rs");
        var method = selectMethod.MakeGenericMethod(typeof (Tuple), newBody.Type);
        lambda = Expression.Lambda<Func<RecordSet, object>>(Expression.Convert(Expression.Call(null, method, rs, Expression.Lambda(newBody, tuple)), typeof(object)), rs);
      }
      return new ResultExpression(body.Type, recordSet, mapping, lambda);
    }

    protected override Expression VisitMemberAccess(MemberExpression m)
    {
      if (translator.Evaluator.CanBeEvaluated(m) && translator.ParameterExtractor.IsParameter(m))
        return m;
      var isEntity = typeof(IEntity).IsAssignableFrom(m.Type);
      var isEntitySet = typeof(EntitySetBase).IsAssignableFrom(m.Type);
      var isStructure = typeof(Structure).IsAssignableFrom(m.Type);
      var isKey = typeof(Key).IsAssignableFrom(m.Type);
      if (isEntity || isEntitySet || isStructure) {
        recordIsUsed = true;
        if (isStructure) {
          // TODO: implement
        }
        else if (isEntity) {
          // TODO: implement
        }
        else {
          // TODO: implement
        }
        throw new NotImplementedException();
      }
      else if (isKey) {
        var keyPath = AccessPath.Parse(m, translator.Model);
        var type = translator.Model.Types[m.Expression.Type];
        var transform = new SegmentTransform(true, type.Hierarchy.KeyTupleDescriptor, source.GetMemberSegment(keyPath));
        var keyExtractor = Expression.Call(keyCreateMethod, Expression.Constant(type),
                                           Expression.Call(Expression.Constant(transform), transformApplyMethod,
                                                           Expression.Constant(TupleTransformType.Auto), tuple),
                                           Expression.Constant(false));
        return keyExtractor;

//        Expression<Func<Tuple, Key>> keyExtractor = t => Key.Create(type, transform.Apply(TupleTransformType.Auto, t), false);

        // TODO: implement
      }
      var path = AccessPath.Parse(m, translator.Model);
      var method = m.Type == typeof(object) ? 
        nonGenericAccessor : 
        genericAccessor.MakeGenericMethod(m.Type);
      var segment = source.GetMemberSegment(path);
      tupleIsUsed = true;
      return Expression.Call(tuple, method, Expression.Constant(segment.Offset));
    }


    // Constructors

    public ProjectionBuilder(QueryTranslator translator)
    {
      this.translator = translator;
    }

    // Type initializer

    static ProjectionBuilder()
    {
      selectMethod = typeof (Enumerable).GetMethods().Where(m => m.Name==WellKnown.Queryable.Select).First();
      keyCreateMethod = typeof (Key).GetMethod("Create", new[] {typeof (TypeInfo), typeof (Tuple), typeof (bool)});
      transformApplyMethod = typeof (SegmentTransform).GetMethod("Apply", new[] {typeof (TupleTransformType), typeof (Tuple)});
      foreach (var method in typeof(Tuple).GetMethods()) {
        if (method.Name == "GetValueOrDefault") {
          if (method.IsGenericMethod)
            genericAccessor = method;
          else
            nonGenericAccessor = method;
        }
      }
    }
  }
}