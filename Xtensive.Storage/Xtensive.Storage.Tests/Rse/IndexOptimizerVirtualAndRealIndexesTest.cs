// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.04.22

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Xtensive.Core.Helpers;
using Xtensive.Core.Linq;
using Xtensive.Core.Testing;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.Rse.VirtualAndRealIndexesModel;

namespace Xtensive.Storage.Tests.Rse
{
  #region Domain model
  namespace VirtualAndRealIndexesModel
  {
    [HierarchyRoot]
    [Index("HierarchyField")]
    public class A : Entity
    {
      [Field, Key]
      public Int32 Id { get; private set; }

      [Field]
      public string HierarchyField { get; set; }
    }

    [Index("ClassField")]
    public class B : A
    {
      [Field]
      public Int32 ClassField { get; set; }
    }
  }
  #endregion

  [TestFixture, Category("Rse")]
  public class IndexOptimizerVirtualAndRealIndexesTest : AutoBuildTest
  {
    protected override void CheckRequirements()
    {
      EnsureIs(StorageProtocols.Index);
    }

    protected override DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.Types.Register(Assembly.GetExecutingAssembly(),
        "Xtensive.Storage.Tests.Rse.VirtualAndRealIndexesModel");
      return config;
    }

    [SetUp]
    public void SetUp()
    {
      FillStorage(300);
    }

    [Test]
    public void CombinedTest()
    {
      using (Domain.OpenSession())
      using (var t = Transaction.Open()) {
        Expression<Func<B, bool>> predicate = b => b.HierarchyField.GreaterThan("k")
          && b.ClassField < int.MaxValue
            || b.HierarchyField.LessThanOrEqual("z") && b.ClassField > int.MaxValue / 2;
        var expected = Query<B>.All.ToList().Where(predicate.CachingCompile()).OrderBy(o => o.Id);
        var query = Query<B>.All.Where(predicate).OrderBy(o => o.Id);
        var actual = query.ToList();
        var virtualIndex = Domain.Model.Types[typeof (B)].Indexes.GetIndex("HierarchyField");
        Assert.IsTrue(virtualIndex.IsVirtual);
        IndexOptimizerTestHelper.ValidateUsedIndex(query, Domain.Model, virtualIndex,
          IndexOptimizerTestHelper.GetIndexForField<B>("ClassField", Domain.Model));
        IndexOptimizerTestHelper.ValidateQueryResult(expected, actual);
      }
    }

    private void FillStorage(int count)
    {
      var random = RandomManager.CreateRandom();
      var stringGenerator = InstanceGeneratorProvider.Default.GetInstanceGenerator<string>();
      var intGenerator = InstanceGeneratorProvider.Default.GetInstanceGenerator<Int32>();
      using (Domain.OpenSession())
      using (var t = Transaction.Open()) {
        for (int i = 0; i < count; i++) {
          new A {HierarchyField = stringGenerator.GetInstance(random)};
          new B
          {
            HierarchyField = stringGenerator.GetInstance(random),
            ClassField = intGenerator.GetInstance(random)
          };
          new B
          {
            HierarchyField = stringGenerator.GetInstance(random),
            ClassField = intGenerator.GetInstance(random)
          };
        }
        t.Complete();
      }
    }
  }
}