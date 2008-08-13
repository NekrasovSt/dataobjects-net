// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2008.07.31

using NUnit.Framework;
using Xtensive.Core;
using Xtensive.Storage.Configuration;

namespace Xtensive.Storage.Tests
{
  [TestFixture]
  public abstract class AutoBuildTest
  {
    private Domain domain;

    protected Domain Domain
    {
      get { return domain; }
    }

    [TestFixtureSetUp]
    public virtual void TestFixtureSetUp()
    {
      DomainConfiguration config = BuildConfiguration();
      domain = BuildDomain(config);
    }

    [TestFixtureTearDown]
    public virtual void TestFixtureTearDown()
    {
    }

    protected virtual DomainConfiguration BuildConfiguration()
    {
      DomainConfiguration config;
      config = DomainConfigurationFactory.Create("memory");
//      config = DomainConfigurationFactory.Create("mssql2005");
//      config = DomainConfigurationFactory.Create("memory", InheritanceSchema.SingleTable);
//      config = DomainConfigurationFactory.Create("memory", InheritanceSchema.SingleTable, TypeIdBehavior.Include);
      return config;
    }

    protected virtual Domain BuildDomain(DomainConfiguration configuration)
    {
      return Domain.Build(configuration);
    }
  }
}