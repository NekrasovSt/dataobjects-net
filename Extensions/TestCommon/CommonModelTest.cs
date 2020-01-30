using System.Collections.Generic;
using NUnit.Framework;
using TestCommon.Model;
using Xtensive.Core;
using Xtensive.Orm;
using Xtensive.Orm.Configuration;
using Xtensive.Orm.Tests;

namespace TestCommon
{
  [TestFixture]
  public abstract class CommonModelTest : AutoBuildTest
  {
    private bool justBuilt = true;

    [SetUp]
    public virtual void SetUp()
    {
      if (justBuilt)
        justBuilt = false;
      else
        RebuildDomain();
    }

    protected override DomainConfiguration BuildConfiguration()
    {
      var configuration = base.BuildConfiguration();
      configuration.Types.Register(typeof (Bar).Assembly);
      return configuration;
    }
  }
}