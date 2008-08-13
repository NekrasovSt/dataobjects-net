// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2007.08.01

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xtensive.Core;
using Xtensive.Core.Collections;
using Xtensive.Core.Diagnostics;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Reflection;
using Xtensive.Core.Tuples;
using Xtensive.Storage.Attributes;
using Xtensive.Storage.Internals;
using Xtensive.Storage.Model;
using Xtensive.Storage.Providers;
using Xtensive.Storage.ReferentialIntegrity;
using Xtensive.Storage.Resources;

namespace Xtensive.Storage
{
  /// <summary>
  /// Principal data objects about which information has to be managed. 
  /// It has a unique identity, independent existence, and forms the operational unit of consistency.
  /// Instance of <see cref="Entity"/> type can be referenced via <see cref="Key"/>.
  /// </summary>
  public abstract class Entity
    : Persistent,
      IEntity
  {
    private static readonly ThreadSafeDictionary<Type, Func<EntityData, Entity>> activators = 
      ThreadSafeDictionary<Type, Func<EntityData, Entity>>.Create(new object());
    private readonly EntityData data;

    #region Internal properties

    [Infrastructure]
    internal EntityData Data {
      [DebuggerStepThrough]
      get { return data; }
    }

    /// <exception cref="Exception">Property is already initialized.</exception>
    [Field]
    internal int TypeId {
      [DebuggerStepThrough]
      get { return GetValue<int>(NameBuilder.TypeIdFieldName); }
      [DebuggerStepThrough]
      private set {
        FieldInfo field = Type.Fields[NameBuilder.TypeIdFieldName];
        field.GetAccessor<int>().SetValue(this, field, value);
      }
    }

    #endregion

    #region Properties: Key, Type, Tuple, PersistenceState

    /// <exception cref="Exception">Property is already initialized.</exception>
    [Infrastructure]
    public Key Key {
      [DebuggerStepThrough]
      get { return Data.Key; }
    }

    /// <inheritdoc/>
    public override sealed TypeInfo Type {
      [DebuggerStepThrough]
      get { return Data.Type; }
    }

    /// <inheritdoc/>
    protected internal sealed override Tuple Tuple {
      [DebuggerStepThrough]
      get { return Data.Tuple; }
    }

    /// <summary>
    /// Gets persistence state of the entity.
    /// </summary>
    [Infrastructure]
    public PersistenceState PersistenceState
    {
      [DebuggerStepThrough]
      get { return Data.PersistenceState; }
      internal set
      {
        if (Data.PersistenceState == value)
          return;
        Data.PersistenceState = value;
        if (PersistenceState != Storage.PersistenceState.Persisted)
          Session.DirtyData.Register(Data);
      }
    }

    #endregion

    #region IIdentifier members

    /// <inheritdoc/>
    [Infrastructure]
    Key IIdentified<Key>.Identifier {
      [DebuggerStepThrough]
      get { return Key; }
    }

    /// <inheritdoc/>
    [Infrastructure]
    object IIdentified.Identifier {
      [DebuggerStepThrough]
      get { return Key; }
    }

    #endregion

    #region Remove method

    /// <inheritdoc/>
    public void Remove()
    {
      if (Log.IsLogged(LogEventTypes.Debug))
        Log.Debug("Session '{0}'. Removing: Key = '{1}'", Session, Key);

      EnsureIsNotRemoved();
      Session.Persist();

      OnRemoving();
      ReferenceManager.ClearReferencesTo(this);
      PersistenceState = PersistenceState.Removed;
      OnRemoved();
    }

    #endregion

    #region Protected event-like methods

    /// <inheritdoc/>
    protected internal override sealed void OnCreating()
    {
      Data.Entity = this;
      Session.DirtyData.Register(Data);
      TypeId = Type.TypeId;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Entity is removed.</exception>
    protected internal override sealed void OnGettingValue(FieldInfo field)
    {
      EnsureIsNotRemoved();
      EnsureIsFetched(field);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Entity is removed.</exception>
    protected internal override sealed void OnSettingValue(FieldInfo field)
    {
      EnsureIsNotRemoved();
    }

    /// <inheritdoc/>
    protected internal override sealed void OnSetValue(FieldInfo field)
    {
      PersistenceState = PersistenceState.Modified;
    }

    [Infrastructure]
    protected virtual void OnRemoving()
    {
    }

    [Infrastructure]
    protected virtual void OnRemoved()
    {
    }

    #endregion

    #region Private \ internal methods

    internal static Entity Activate(Type type, EntityData data)
    {
      return activators.GetValue(type, 
        DelegateHelper.CreateConstructorDelegate<Func<EntityData, Entity>>)
        .Invoke(data);
    }

    [Infrastructure]
    private void EnsureIsFetched(FieldInfo field)
    {
      if (Session.DirtyData.GetItems(PersistenceState.New).Contains(Data))
        return;
      if (Data.Tuple.IsAvailable(field.MappingInfo.Offset))
        return;
      Fetcher.Fetch(Key, field);
    }

    /// <exception cref="InvalidOperationException">[Suppresses warning]</exception>
    [Infrastructure]
    private void EnsureIsNotRemoved()
    {
      if (PersistenceState==PersistenceState.Removed)
        throw new InvalidOperationException(Strings.ExEntityIsRemoved);
    }

    #endregion


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    protected Entity()
    {
      Key key = Session.Domain.KeyManager.Next(GetType());

      if (Log.IsLogged(LogEventTypes.Debug))
        Log.Debug("Session '{0}'. Creating: Key = '{1}'", Session, key);

      data = Session.DataCache.Create(key, PersistenceState.New);
      OnCreating();
    }

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="tuple">The <see cref="Tuple"/> that will be used for key building.</param>
    /// <remarks>Use this kind of constructor when you need to explicitly build key for this instance.</remarks>
    protected Entity(Tuple tuple)
    {
      Key key = Session.Domain.KeyManager.Get(GetType(), tuple);

      if (Log.IsLogged(LogEventTypes.Debug))
        Log.Debug("Session '{0}'. Creating: Key = '{1}'", Session, key);

      data = Session.DataCache.Create(key, PersistenceState.New);
      OnCreating();
    }

    /// <summary>
    /// <see cref="ClassDocTemplate()" copy="true"/>
    /// </summary>
    /// <param name="data">The initial data of this instance fetched from storage.</param>
    protected Entity(EntityData data)
      : base(data)
    {
      this.data = data;

      if (Log.IsLogged(LogEventTypes.Debug))
        Log.Debug("Session '{0}'. Creating: Key = '{1}'", Session, Key);
    }
  }
}