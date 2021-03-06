// Copyright (C) 2016-2020 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.
// Created by: Alexey Kulakov
// Created:    2016.10.19

using System;
using System.Collections.Generic;
using Xtensive.Orm.Upgrade;

namespace Xtensive.Orm.Tests.Upgrade.HugeModelUpgrade
{
  public class UpgradePerformanceCounter : UpgradeHandler
  {
    private readonly Dictionary<string, long> memoryUsages = new Dictionary<string, long>();

    public override bool IsEnabled => true;

    public override bool CanUpgradeFrom(string oldVersion) => true;

    public override void OnPrepare()
    {
      memoryUsages.Add("OnPrepare                           ", GC.GetTotalMemory(false));
      base.OnPrepare();
    }

    public override void OnBeforeStage()
    {
      memoryUsages.Add("OnBeforeStage                       ", GC.GetTotalMemory(false));
      base.OnBeforeStage();
    }

    public override void OnUpgrade()
    {
      memoryUsages.Add("OnUpgrade before base               ", GC.GetTotalMemory(false));
      base.OnUpgrade();
    }

    public override void OnStage()
    {
      memoryUsages.Add("OnStage before base                 ", GC.GetTotalMemory(false));
      base.OnStage();
    }

    public override void OnSchemaReady()
    {
      memoryUsages.Add("OnSchemaReady before base           ", GC.GetTotalMemory(false));
      base.OnSchemaReady();
    }

    public override void OnComplete(Domain domain)
    {
      memoryUsages.Add("OnComplete                          ", GC.GetTotalMemory(false));
      memoryUsages.Add("OnComplete after garbage collection ", GC.GetTotalMemory(true));

      var container = domain.Extensions.Get<PerformanceResultContainer>();
      if (container == null) {
        container = new PerformanceResultContainer();
        domain.Extensions.Set(typeof(PerformanceResultContainer), container);
      }
      container.Add(UpgradeContext.StorageNode.Id, memoryUsages);
    }
  }
}
