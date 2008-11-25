// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2008.11.25

using System.Reflection;
using NUnit.Framework;
using Xtensive.Storage.Attributes;
using Xtensive.Storage.Tests.Bug0007_Model;

namespace Xtensive.Storage.Tests.Bug0007_Model
{
  [HierarchyRoot(typeof (KeyGenerator), "ID")]
  public class Person : Entity
  {
    [Field]
    public int ID { get; private set; }

    [Field]
    public Address Address { get; set; }
  }

  public class Address : Structure
  {
    [Field]
    public string Street { get; set; }
  }
}

namespace Xtensive.Storage.Tests.BugReports
{
  public class Bug0007_InvalidNotNullConstraint : AutoBuildTest
  {
    protected override Xtensive.Storage.Configuration.DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.Types.Register(Assembly.GetExecutingAssembly(), typeof(Person).Namespace);
      return config;
    }

    [Test]
    public void MainTest()
    {
      Assert.AreEqual(true, Domain.Model.Types[typeof (Person)].Fields["Address.Street"].IsNullable);
    }
  }
}