// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2010.06.24

using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using Xtensive.Core;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Services;

namespace Xtensive.Storage.Tests.Storage.EntitySetEventsTest
{
  [Serializable]
  [HierarchyRoot]
  public class Book : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field]
    public string Title { get; set; }

    [Field]
    public EntitySet<Book> RelatedBooks { get; private set; }

    public override string ToString()
    {
      return Title;
    }
  }

  [TestFixture]
  public class NotifyXxxTest : AutoBuildTest
  {
    private NotifyCollectionChangedAction lastChangeAction;
    private string lastChangedProperty;
    private object lastSenderObject;
    private object lastSenderCollection;

    protected override DomainConfiguration BuildConfiguration()
    {
      var configuration = base.BuildConfiguration();
      configuration.Types.Register(typeof(Book).Assembly, typeof(Book).Namespace);
      return configuration;
    }

    [Test]
    public void CombinedTest()
    {
      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var sessionStateAccessor = DirectStateAccessor.Get(session);
        var book1 = new Book() {Title = "Book 1"};
        var book2 = new Book() {Title = "Book"};
        book1.RelatedBooks.CollectionChanged += RelatedBooks_CollectionChanged;
        book2.PropertyChanged += Book_PropertyChanged;

        ResetLastXxx();
        book2.Title = "Book 2";
        Assert.AreEqual("Title", lastChangedProperty);
        Assert.AreSame(book2, lastSenderObject);

        ResetLastXxx();
        book1.RelatedBooks.Add(book2);
        Assert.AreEqual(NotifyCollectionChangedAction.Add, lastChangeAction);
        Assert.AreSame(book1.RelatedBooks, lastSenderCollection);

        ResetLastXxx();
        book1.RelatedBooks.Remove(book2);
        Assert.AreEqual(NotifyCollectionChangedAction.Remove, lastChangeAction);
        Assert.AreSame(book1.RelatedBooks, lastSenderCollection);

        ResetLastXxx();
        book1.RelatedBooks.Clear();
        Assert.AreEqual(NotifyCollectionChangedAction.Reset, lastChangeAction);
        Assert.AreSame(book1.RelatedBooks, lastSenderCollection);

        ResetLastXxx();
        session.NotifyChanged();
        Assert.AreEqual(null, lastChangedProperty);
        Assert.AreSame(book2, lastSenderObject);
        Assert.AreEqual(NotifyCollectionChangedAction.Reset, lastChangeAction);
        Assert.AreSame(book1.RelatedBooks, lastSenderCollection);
        // tx.Complete();
      }
    }

    private void ResetLastXxx()
    {
      lastChangeAction = NotifyCollectionChangedAction.Move; // Since it is never used by DO4
      lastChangedProperty = "@None@";
      lastSenderObject = new object();
      lastSenderCollection = new object();
    }

    private void Book_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      Log.Info("PropertyChanged: Sender = {0}, Property = {1}", sender, e.PropertyName);
      lastSenderObject = sender;
      lastChangedProperty = e.PropertyName;
    }

    private void RelatedBooks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      Log.Info("CollectionChanged: Sender = {0}, Action = {1}", sender, e.Action);
      lastSenderCollection = sender;
      lastChangeAction = e.Action;
    }
  }
}
