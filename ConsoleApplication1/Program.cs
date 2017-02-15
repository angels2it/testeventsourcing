using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using MongoDB.Bson.Serialization;
using NEventStore;
using NEventStore.Serialization;
using TinyMessenger;

namespace ConsoleApplication1
{
    class Program
    {
        private static readonly TimeSpan ProductionTime = TimeSpan.FromSeconds(30);
        private static Guid _castleId = new Guid("b7113001-80d4-ec4b-87e0-6b94fcc18694");
        private static Guid _gameId = new Guid("b7113001-80d4-ec4b-87e0-6b94fcc18695");
        private static IStoreEvents _store;
        public static readonly TinyMessengerHub MessageHub = new TinyMessengerHub();
        private static IContainer Container { get; set; }
        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args)
        {
            var build = new ContainerBuilder();
            build.RegisterType<CreateSoldierEventHandler>().AsImplementedInterfaces();
            Container = build.Build();
            BsonInit();
            EventHandler.Init(Container);
            TinyInit();
            EventStoreInit();
            while (true)
            {
                var text = Console.ReadLine();
                if (text == "CreateGame")
                {
                    CreateGame();
                }
                if (text == "Build")
                    BuildUserInf();
            }
            // ReSharper disable once FunctionNeverReturns
        }

        private static void CreateGame()
        {
            using (IEventStream stream = _store.OpenStream(_gameId, 0))
            {
                // create game
                var @event = new CreateGameEvent();
                stream.Add(new EventMessage { Body = @event });
                stream.CommitChanges(Guid.NewGuid());
            }
            CreateCastle(_castleId);
        }

        private static void CreateCastle(Guid castleId)
        {
            Console.WriteLine("Create castle");
            using (IEventStream stream = _store.OpenStream(castleId, 0))
            {
                // create game
                var @event = new CreateCastleEvent();
                stream.Add(new EventMessage { Body = @event });
                stream.CommitChanges(Guid.NewGuid());
            }
            CreateSoldier();
        }

        private static void CreateSoldier()
        {
            Console.WriteLine("Create soldier");
            using (IEventStream stream = _store.OpenStream(_castleId, 0))
            {
                var @event = new CreateSoldierEvent(Guid.NewGuid(), ProductionTime);

                stream.Add(new EventMessage { Body = @event });
                stream.CommitChanges(Guid.NewGuid());
            }
        }

        private static void BuildUserInf()
        {
            Console.WriteLine("Building user info...");
            BuildGameInfo(_gameId);
        }

        private static void BuildGameInfo(Guid gameId)
        {
            using (var stream = _store.OpenStream(gameId.ToString()))
            {
                if (stream.StreamRevision == 0)
                {
                    Console.WriteLine("No any game to build");
                    return;
                }
                Console.WriteLine("Building game info...");
                BuildCastle(_castleId);
            }
        }

        private static void EventStoreInit()
        {
            var w = Wireup.Init()
                .LogToOutputWindow()
                .UsingMongoPersistence("connect", new DocumentObjectSerializer())
                .InitializeStorageEngine();
            WireupExtensions.UsingBsonSerialization(w);
            _store = w.Build();
        }

        private static void BsonInit()
        {
            // event
            var types = Assembly.GetAssembly(typeof(DomainEvent))
                    .GetTypes()
                    .Where(type => type.IsSubclassOf(typeof(DomainEvent)));
            foreach (var t in types)
                BsonClassMap.LookupClassMap(t);
            // snapshot
            types = Assembly.GetAssembly(typeof(Aggregate))
                    .GetTypes()
                    .Where(type => type.IsSubclassOf(typeof(Aggregate)));
            foreach (var t in types)
                BsonClassMap.LookupClassMap(t);
        }


        private static void TinyInit()
        {
            MessageHub.Subscribe<SoldierCreatedEvent>(OnSoldierCreateEvent);
        }
        private static void BuildCastle(Guid castleId)
        {
            Console.WriteLine("Start building castle state...");
            var latestSnapshot = _store.Advanced.GetSnapshot(castleId.ToString(), int.MaxValue);
            var gameSnapshot = latestSnapshot?.Payload as CastleAggregate ?? new CastleAggregate()
            {
                Id = castleId,
                SoldiersAmount = 0,
                Soldiers = new List<Guid>()
            };
            using (IEventStream stream = _store.OpenStream(castleId, latestSnapshot?.StreamRevision ?? 0))
            {
                var oldSnapVersion = latestSnapshot?.StreamRevision ?? 0;
                var unBuildVersion = stream.StreamRevision - (latestSnapshot?.StreamRevision ?? 0);
                var start = latestSnapshot == null ? 0 : 1;
                var newSnapVersion = oldSnapVersion;
                for (int i = start; i < unBuildVersion + start; i++)
                {
                    var @event = stream.CommittedEvents.ElementAt(i).Body as DomainEvent;
                    if (@event == null || @event.RunningAt.CompareTo(DateTime.UtcNow) > 1)
                        break;
                    var d1 = typeof(DomainEventHandlerData<>);
                    Type[] typeArgs = { @event.GetType() };
                    var makeme = d1.MakeGenericType(typeArgs);
                    if (!EventHandler.Publish(new DomainEventHandlerData<DomainEvent>()
                    {
                        Store = _store,
                        Event = @event,
                        Snapshot = gameSnapshot,
                        Id = _castleId
                    }))
                        continue;
                    //if (@event.GetType().IsAssignableFrom(typeof(CreateSoldierEvent)))
                    //    if (!EventHandler.Publish(new DomainEventHandlerData<CreateSoldierEvent>()
                    //    {
                    //        Store = _store,
                    //        Event = @event as CreateSoldierEvent,
                    //        Snapshot = gameSnapshot,
                    //        Id = _castleId
                    //    }))
                    //        continue;
                    newSnapVersion++;
                }
                if (newSnapVersion > oldSnapVersion)
                {
                    Console.WriteLine($"Snap version: {newSnapVersion}");
                    _store.Advanced.AddSnapshot(new Snapshot(castleId.ToString(), newSnapVersion, gameSnapshot));
                    // snapshot again
                    BuildCastle(castleId);
                }
                else
                {
                    Console.WriteLine($"Soldiers amount: {gameSnapshot.SoldiersAmount}");
                }
            }
        }

        private static void OnSoldierCreateEvent(SoldierCreatedEvent createSoldier)
        {
            using (IEventStream stream = _store.OpenStream(_castleId))
            {
                var @event = new CreateSoldierEvent(Guid.NewGuid(), ProductionTime);

                stream.Add(new EventMessage { Body = @event });
                stream.CommitChanges(Guid.NewGuid());
            }
        }
    }
}
